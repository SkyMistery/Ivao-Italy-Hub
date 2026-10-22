# La revisione del codice, per lotti

> Il piano della revisione di tutto il codice (nota `decisions/2026-10-20-la-revisione-del-codice.md`). Accanto a ogni lotto, lo stato:
> ⬜ da fare · 🔎 in lettura · ✅ letto e verificato, con i numeri delle issue (e «advisory» senza il numero, finché è privato).
> Questo file lo aggiorna il master a ogni lotto.

## 1. I lotti

| # | Lotto | Dove | Righe | Stato |
|---|---|---|---|---|
| 1 | **Permessi**: l'unico gestore, il catalogo, i permessi effettivi, i grant e il loro ambito, il filtro globale, la guardia dell'interceptor, `MapCrud` e la lista generata, `/api/me` | `Core/Auth/Permissions/`, `Core/Auth/` (grant, posizioni, superadmin, `ICurrentUser`, `HubClaims`, sincronizzazione), `Core/Data/` (`HubSaveChangesInterceptor`, `VisibilityQueryFilter`, `Crud/`), `Web/Endpoints/MeEndpoints.cs` | ~8 000 | ✅ advisory, #319 |
| 2 | **Accesso e token**: OIDC di IVAO, cookie, logout, CSRF, token personali e contratti, token dei job, header del proxy, limiti di frequenza, header di sicurezza | `Core/Auth/Ivao*.cs`, `PersonalToken*`, `ContractVersion`, `SecurityStampCache`, `Web/` (`HubPipeline`, `SecurityHeaders`, `AuthEndpoints`, `JobTokenLogMask`, `E2E/`), `Core/Services/MemberWriteLimit.cs` | ~4 000 | ✅ advisory, #320 |
| 3 | **Contenuti, blocchi e file**: pubblicazione e versioni, walker, blocchi Data, blocco interattivo, upload e media, indirizzi, ricerca, proiezioni | `Core/Content/`, `Data/FullTextSearch.cs`, `web/src/blocks/` | ~15 000 | ✅ advisory, #317, #318, #321 |
| 4 | **Job, rete e avvio**: job e il loro recupero, client IVAO e sincronizzazioni, spazio aereo, meteo, inizializzazione e migrazioni, diagnostica | `Core/Jobs/`, `Core/Ivao/`, `Core/Airspace/`, `Core/Weather/`, `Core/Atc/`, `Core/Services/` (avvio, blocchi, tempi), `Data/HubDatabaseInitializer.cs` | ~10 000 | ✅ #324, #325 |
| 5 | **Persone e tracce**: cancellazione dei dati, audit e conservazione, notifiche e mail, award, preferenze, divisione | `Core/Privacy/`, `Core/People/`, `Services/Audit*`, `Core/Notifications/`, `Core/Awards/`, `Core/Preferences/`, `Core/Division/`, `Core/Localization/`, `Core/Modules/` | ~5 500 | ✅ #323, #326 |
| 6 | **Eventi, server** | `src/IvaoHub.Modules.Events/` | ~22 000 | ✅ #322 |
| 7 | **Training, server** | `src/IvaoHub.Modules.Training/` | ~7 400 | ✅ #327 |
| 8 | **Tour, server** | `src/IvaoHub.Modules.FlightOps/` | ~18 000 | ✅ advisory, #328 |
| 9 | **Web del nucleo**: editor, liste e form generati, `/api/me`, router, pagine pubbliche e staff | `web/src/features/`, `web/src/shared/`, `web/src/routes/` | ~47 000 | ✅ #329–#332 |
| 10 | **Web dei moduli** | `web/src/modules/events/`, `training/`, `flightops/` | ~32 000 | ✅ #331, #333, #334 |

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

Il primo giro (20–21 ottobre 2026, lotti 1, 2, 3 e 6 in parallelo) ha prodotto tre advisory privati, uno per ciascuno di questi temi:
contenuti pubblicati che mostrano campi mai pubblicati (lotto 3), sessioni che non scadono (lotti 1 e 2, trovato due volte), grant su una
riga e liste del dipartimento (lotto 1, con una domanda a Carmine).

- **Lotto 1**: 12 rilievi; uno nell'advisory dei grant, uno in quello delle sessioni, gli altri dieci in #319 (minori, uno ⚖️ per Carmine).
- **Lotto 2**: 10 rilievi; il primo è lo stesso dell'advisory delle sessioni e il logout è stato messo lì; il resto in #320 (minori).
- **Lotto 3**: 13 rilievi; due nell'advisory dei contenuti, #317 e #318 (major), sette in #321 (minori), una pagina enorme di ricerca è
  in #319.
- **Lotto 6**: 5 rilievi, tutti minori, in #322 a `dalberone`; nessun blocker, niente di nuovo sui dati altrui.

Il secondo giro (21 ottobre 2026, lotti 4, 5, 7 e 8 in parallelo) ha allargato un advisory privato già aperto, quello delle mail dei
membri senza freno: un verbo dei Tour (la segnalazione di un leg) che il limite della 0.10.1 non raggiunge.

- **Lotto 4**: 8 rilievi; il client IVAO che lancia dove promette `null` in #324 (major), gli altri sette in #325 (minori).
- **Lotto 5**: 9 rilievi; il ban tenuto e gli IP degli accessi che riportano alla persona cancellata in #323 (major), sei in #326
  (minori), la mail della cessione di Eventi aggiunta a #322.
- **Lotto 7**: 5 rilievi, tutti minori, in #327 a `dalberone` (uno ⚖️ per Carmine); nessun blocker.
- **Lotto 8**: 13 rilievi; uno nell'advisory delle mail, il job dei controlli cieco sui rapporti precedenti e gli altri undici in #328
  (due ⚖️ per Carmine).

Il terzo giro (21 ottobre 2026, lotti 9 e 10; il 9 diviso fra due letture, `features/` + `routes/` e `shared/`) non ha trovato niente di
sfruttabile. Uno dei tre major è stato visto anche sulla prova, con una sola lettura senza accesso: `/staff` porta a «pagina non
trovata» invece che all'accesso di IVAO.

- **Lotto 9**: 21 rilievi; l'accesso dalle pagine protette (#329), l'editor che sovrascrive un altro redattore (#330) e il form che nasconde
  i rifiuti senza campo (#331) sono major; il permesso del blocco interattivo era già #318; gli altri in #332 (minori).
- **Lotto 10**: 8 rilievi; i rifiuti di Eventi che non si vedono sono la faccia di #331; Eventi e Training in #333 a `dalberone`, Tour in
  #334 (minori).

**Tutti e dieci i lotti sono letti.** Quello che resta è nelle issue e negli advisory: la correzione, e le domande ⚖️ a Carmine.

## 5. Le correzioni

Le domande ⚖️ hanno tutte una risposta di Carmine (21 ottobre 2026, in chat al master, «Tutte come suggerisci tu»), scritta come
«Maintainer's answer» sulla issue, sulla PR o nell'avviso. Lo stato al 22 ottobre 2026, sera (piano 1.43):

| Rilievo | Correzione | Stato |
|---|---|---|
| Avviso GHSA-4jch (campi non pubblicati che escono) + #317 | #339, `2026-10-21-il-pubblico-legge-la-versione` | ✅ 0.10.5, avviso pubblicato |
| I campi fuori dalla versione (la 2 di Carmine) | #348, `2026-10-21-i-campi-fuori-dalla-versione` | ✅ in `main` con la 0.11.0, non ancora sulla prova |
| Avviso GHSA-9h6p (la sessione che non scade) + il logout | #338 | ✅ 0.10.5, avviso pubblicato |
| Avviso GHSA-rxxv (un grant su una riga e le liste) | #343, `2026-10-21-il-grant-su-una-riga-elenca-le-sue-righe` | ✅ 0.10.5, avviso pubblicato |
| Avviso GHSA-c964 (le mail dei membri) | #315 (0.10.1) e #340 (tutti i verbi dei membri) | ✅ 0.10.5, avviso pubblicato |
| #318 `Content.EmbedCode` solo nel browser | #341 | ✅ 0.10.5 |
| #323 la cancellazione e l'audit | #342, `2026-10-21-le-tracce-della-persona-nell-audit` | ✅ 0.10.5 |
| #324 il client IVAO che lancia, #325 punto 1 | #344, `2026-10-21-il-client-ivao-non-lancia` | ✅ 0.10.5 |
| #328 punto 1 il job dei controlli | #337 | ✅ 0.10.3 |
| #329 l'accesso dalle pagine protette | #335 | ✅ 0.10.2 |
| #330 l'editor che sovrascrive, #332 punti 7 e 8 | #346 | ✅ in `main` con la 0.10.6, non ancora sulla prova |
| #331 il form che nasconde i rifiuti, #332 punto 1 | #347 | ✅ in `main` con la 0.10.6, non ancora sulla prova |
| #319 punto 2, #328 punti 5 e 7 | — | ⬜ decise, senza codice |
| Minori #319–#321, #325, #326, #328, #332, #334 | — | ⬜ |
| `dalberone`: #311–#314, #322, #327, #333, avviso GHSA-h2hw | — | ⬜ |
