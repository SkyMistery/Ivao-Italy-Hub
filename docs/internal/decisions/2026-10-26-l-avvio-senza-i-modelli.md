# L'avvio senza i modelli dei moduli

**Data:** 26 ottobre 2026
**Stato:** **proposta**, già nel codice di questa PR: è la strada scritta nella issue
[#398](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/398) («Possible direction»), misurata. Nessuna scelta a bivio con
un costo nuovo (i modelli compilati restano esclusi, §3): Carmine la conferma o la rifiuta al merge.

Versione **0.12.3**, PATCH: una correzione di velocità, nessuna migrazione, nessuna pagina.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il marcatore d'inizializzazione
(`2026-09-28-il-marcatore-d-inizializzazione.md`, `2026-10-05-l-inizializzazione-sotto-blocco.md`). Nessun meccanismo nuovo,
nessuna tabella. Cambio del nucleo, nella sua PR.

## 1. Il problema

Dall'analisi della velocità del sito di prova (`docs/internal/test/velocita-del-sito.md` §3.2, 186 avvii della 0.10.5): il passo
`models` di `InitializeAsync` costa **2,0 s di mediana (p90 4,4 s), il 35 % di un avvio**, e Passenger riavvia l'hub circa 50
volte l'ora. Il passo costruisce il modello EF di **ogni** contesto (nucleo, Tours, Training, Events) a ogni avvio, per tre
controlli delle marcature (`VerifyAlternatives`, `VerifyWithdrawals`, `VerifyErasedWith`) e per imparare le aree con un FIR
(`LearnAreasWithAFir`). L'esito dei tre controlli dipende solo dal codice e dai moduli abilitati: gli stessi ingressi della
chiave del marcatore. Le migrazioni non c'entrano: un risveglio le salta già dalla 0.2.7.

## 2. La decisione

- **I tre controlli girano dentro l'inizializzazione**, come primo passo (`models`), prima delle migrazioni e quindi prima di
  ogni scrittura, come prima. `models` entra in `SkippableSteps`: un avvio con un pacchetto nuovo, una configurazione nuova o
  un `seed/` nuovo li fa tutti; un risveglio dello stesso pacchetto, che li ha già passati con la stessa chiave, li salta.
  La regola «una marcatura che la guardia non saprebbe onorare ferma l'avvio» resta vera per ogni build nuova: una build
  con una marcatura sbagliata non scrive mai il segno, e ogni suo avvio la ritrova.
- **Le aree con un FIR, a un risveglio, si leggono al primo bisogno**: `PermissionCatalog.LearnAreasWithAFir` ha una seconda
  forma che prende una funzione, chiamata la prima volta che qualcuno chiede `IsOfAnAreaWithAFir` (il validatore di un grant
  alla squadra di un FIR; il seeder dei `positionGrants` gira solo nell'inizializzazione, che le impara subito). Una lettura
  che fallisce si ripete alla domanda dopo (`LazyThreadSafetyMode.PublicationOnly`), non resta in memoria come errore.
- **Il modello del nucleo si costruisce comunque**: lo usa la lettura del marcatore, la prima query. Quello di un modulo lo
  costruisce la prima richiesta che usa il modulo (EF lo tiene per tutta la vita del processo, come prima).

## 3. Le alternative

- **Leggere le tabelle `__EFMigrationsHistory_<modulo>` invece di costruire i modelli**: non tocca il passo lento. Le
  migrazioni si saltano già col marcatore; i modelli servono ai controlli, non alle migrazioni.
- **Modelli EF compilati** (`dotnet ef dbcontext optimize`): esclusi (`2026-09-28-l-avvio-a-freddo.md` §4.2: EF li rifiuta con i
  filtri globali di query e con le funzioni tradotte da noi). Avrebbero anche un costo fisso: file generati per contesto, da
  rigenerare a ogni cambio del modello, e un controllo in CI che ne trovi uno vecchio.
- **Salvare le aree con un FIR nel segno del marcatore**: corretto, ma lega al marcatore un dato di un altro meccanismo e
  cambia il formato del segno. La lettura al primo bisogno tocca solo il catalogo.
- **Le aree dalle proprietà `DbSet<T>` dei contesti, per riflessione**: costerebbe pochi millisecondi, ma introduce una regola
  nuova («un'entità con un FIR ha un `DbSet`») che oggi nessuno scrive. Scartata.

## 4. Le misure

Il pacchetto vero (`dotnet publish -c Release -r linux-x64 --self-contained`, SDK 10, ReadyToRun) di `main` (`8809066`) e di
questo ramo, avviato in un contenitore `aspnet:8.0` con **una CPU** (`--cpus=1`), `ASPNETCORE_ENVIRONMENT=Production`, contro
MariaDB 11.4.10 con il database già migrato e seminato e due centri in `ref_ivao_centers`; un avvio per contenitore, prima un
avvio completo per scrivere il segno e poi i risvegli. Mediane di 7 risvegli, da `diagnostics/starts.txt`:

| | prima | dopo |
|---|---:|---:|
| `models` | 898 ms | — (saltato) |
| `marker` (prima query: ora costruisce il modello del nucleo) | 377 ms | 875 ms |
| **pronto** (`ready in`) | **3,21 s** | **2,84 s** |
| **prima risposta** (`first answer at`) | **4,11 s** | **3,81 s** |
| `GET /api/flightops/tours/public`, la prima richiesta di un modulo | 68 ms | 225 ms |

Un'altra serie (9 risvegli per parte) ha dato pronto 3,25 → 3,10 s, con `models` + `marker` 1 284 → 924 ms. Il guadagno è
quello dei tre modelli dei moduli, **~0,37 s su una CPU**, il 40 % del passo; il modello del nucleo (~0,5 s) resta. Sul server,
che va circa 2,3–2,7 volte più piano del banco, vuol dire **~0,9 s per avvio**, ~50 volte l'ora. Il conto si sposta in parte:
la prima richiesta di un modulo paga il suo modello (+0,16 s sul banco per Tours, ~0,4 s sul server); una visita che non tocca
moduli non lo paga mai. Un avvio completo costa uguale a prima (i controlli girano tutti).

## 5. Che cosa resta come prima

- Le migrazioni girano all'avvio quando ce ne sono (`CLAUDE.md` §6): il segno cambia con il pacchetto, e un pacchetto nuovo fa
  l'inizializzazione completa. Una catena applicata a metà non scrive il segno, e l'avvio dopo rifà tutto.
- Due processi insieme: i controlli girano sotto il blocco dell'inizializzazione, come il resto.
- `starts.txt`: il passo `models` compare solo negli avvii completi, e la riga di un risveglio lo elenca tra i saltati
  (`initialisation skipped (…): models, migrations, module migrations, position grants, content`).

## Da portare nel piano

- **§11.3 (il marcatore d'inizializzazione)**: tra i passi che un risveglio salta c'è anche la costruzione dei modelli dei
  moduli con i loro controlli (`VerifyAlternatives`, `VerifyWithdrawals`, `VerifyErasedWith`), che ora girano solo nell'
  inizializzazione completa; le aree con un FIR si leggono al primo bisogno. Corregge la riga «gira sempre: la costruzione dei
  modelli con `VerifyAlternatives`» della nota del marcatore (§4.3 della nota dell'avvio a freddo).
- **§11.3 punto 2**: in `starts.txt` il passo `models` solo negli avvii completi; `marker` di un risveglio include il modello
  del nucleo.
- Le misure del §4, accanto a quelle del 28 settembre.
