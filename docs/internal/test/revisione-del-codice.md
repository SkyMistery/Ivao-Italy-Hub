# La revisione del codice, per lotti

> Il piano della revisione di tutto il codice (nota `decisions/2026-10-20-la-revisione-del-codice.md`). Accanto a ogni lotto, lo stato:
> ⬜ da fare · 🔎 in lettura · ✅ letto e verificato, con i numeri delle issue (e «advisory» senza il numero, finché è privato).
> Questo file lo aggiorna il master a ogni lotto.

## 1. I lotti

| # | Lotto | Dove | Righe | Stato |
|---|---|---|---|---|
| 1 | **Permessi**: l'unico gestore, il catalogo, i permessi effettivi, i grant e il loro ambito, il filtro globale, la guardia dell'interceptor, `MapCrud` e la lista generata, `/api/me` | `Core/Auth/Permissions/`, `Core/Auth/` (grant, posizioni, superadmin, `ICurrentUser`, `HubClaims`, sincronizzazione), `Core/Data/` (`HubSaveChangesInterceptor`, `VisibilityQueryFilter`, `Crud/`), `Web/Endpoints/MeEndpoints.cs` | ~8 000 | 🔎 |
| 2 | **Accesso e token**: OIDC di IVAO, cookie, logout, CSRF, token personali e contratti, token dei job, header del proxy, limiti di frequenza, header di sicurezza | `Core/Auth/Ivao*.cs`, `PersonalToken*`, `ContractVersion`, `SecurityStampCache`, `Web/` (`HubPipeline`, `SecurityHeaders`, `AuthEndpoints`, `JobTokenLogMask`, `E2E/`), `Core/Services/MemberWriteLimit.cs` | ~4 000 | 🔎 |
| 3 | **Contenuti, blocchi e file**: pubblicazione e versioni, walker, blocchi Data, blocco interattivo, upload e media, indirizzi, ricerca, proiezioni | `Core/Content/`, `Data/FullTextSearch.cs`, `web/src/blocks/` | ~15 000 | 🔎 |
| 4 | **Job, rete e avvio**: job e il loro recupero, client IVAO e sincronizzazioni, spazio aereo, meteo, inizializzazione e migrazioni, diagnostica | `Core/Jobs/`, `Core/Ivao/`, `Core/Airspace/`, `Core/Weather/`, `Core/Atc/`, `Core/Services/` (avvio, blocchi, tempi), `Data/HubDatabaseInitializer.cs` | ~10 000 | ⬜ |
| 5 | **Persone e tracce**: cancellazione dei dati, audit e conservazione, notifiche e mail, award, preferenze, divisione | `Core/Privacy/`, `Core/People/`, `Services/Audit*`, `Core/Notifications/`, `Core/Awards/`, `Core/Preferences/`, `Core/Division/`, `Core/Localization/`, `Core/Modules/` | ~5 500 | ⬜ |
| 6 | **Eventi, server** | `src/IvaoHub.Modules.Events/` | ~22 000 | 🔎 |
| 7 | **Training, server** | `src/IvaoHub.Modules.Training/` | ~7 400 | ⬜ |
| 8 | **Tour, server** | `src/IvaoHub.Modules.FlightOps/` | ~18 000 | ⬜ |
| 9 | **Web del nucleo**: editor, liste e form generati, `/api/me`, router, pagine pubbliche e staff | `web/src/features/`, `web/src/shared/`, `web/src/routes/` | ~47 000 | ⬜ |
| 10 | **Web dei moduli** | `web/src/modules/events/`, `training/`, `flightops/` | ~32 000 | ⬜ |

Le righe sono del codice senza test né migrazioni (conto del 20 ottobre 2026).

## 2. Che cosa si cerca

La lista di sicurezza (`sicurezza-lista-di-controllo.md`) per le falle, e questa per i difetti:

- **Permessi e dati altrui**: una riga letta o scritta da chi non deve, una lista che conta o mostra righe che il dettaglio negherebbe,
  un campo scritto dal client che il server dovrebbe decidere.
- **Transazioni**: un verbo che salva in due tempi e si ferma a metà, una proiezione o un audit fuori dalla stessa transazione, un
  errore inghiottito dopo una scrittura.
- **Concorrenza**: due richieste insieme sulla stessa riga (due prenotazioni dell'ultimo posto, due decisioni), un controllo letto prima
  e scritto dopo senza blocco né versione.
- **Tempo**: UTC contro ora locale, il giorno «di oggi» in un fuso, i confini inclusi o esclusi, `DateTime.Now` al posto di `IClock`.
- **Limiti**: liste, array e corpi senza tetto, paginazione che si può forzare, cicli sul database dentro un ciclo.
- **Stati**: una transizione che il server permette e il disegno no (riaprire una cosa chiusa, decidere due volte), uno stato finale
  che un altro verbo ancora tocca.
- **Errori**: un caso che risponde 500 invece di un rifiuto, un `null` non previsto, un errore che porta fuori dettagli del server.
- **Migrazioni**: non additive, o che si fermano su dati esistenti.
- **Client** (lotti 9–10): uno stato che non si aggiorna dopo un salvataggio, una pagina che decide un permesso che il server non
  ripete, XSS, testo non tradotto.

## 3. Come esce un rilievo

1. L'agente lo scrive con `file:riga`, lo scenario concreto (dati e passi → che cosa succede di sbagliato), la gravità che propone
   (`blocker` / `major` / `minor`) e quanto ne è sicuro.
2. Il master lo verifica nel codice: confermato, plausibile (da provare in esecuzione), o scartato con il motivo.
3. Confermato: una issue col modello `bug.yml` e le etichette del modulo e della gravità, o un **advisory privato** se è sfruttabile.
4. Il proprietario corregge con una PR `fix/<slug>` e «Closes #N»; il master la rivede come sempre.

## 4. Gli esiti

Qui sotto, per ogni lotto chiuso, una riga: quanti rilievi letti, quanti confermati, con i numeri.
