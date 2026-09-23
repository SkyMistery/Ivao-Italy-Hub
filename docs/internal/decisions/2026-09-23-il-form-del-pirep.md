# Il form del PIREP: una pagina dietro il login, la procedura la decide il server, il banco rivola un volo registrato

**Data:** 23 settembre 2026 — fase **T11b** di M2 (il form, i colori della mappa, «Invia il report», i PIREP del pilota)
**Stato:** **decisa** senza domande: sono tutte scelte dentro il design (§3.2, §3.4, §8.1) e la nota `2026-09-23-il-pirep`,
più un'estensione piccola di un meccanismo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)** per il manifest dei moduli, caso (a) per il resto.

## 1. ⚠️ Estensione: `area: 'member'` nel manifest di un modulo

Il form è `/tours/{slug}/report` (nota `il-pirep` §4) e ha senso solo con il login: le sessioni del tracker sono del pilota. Un
modulo aveva due aree, `public` (sotto `_public`, senza guardia) e `staff` (sotto `_staff`, con il permesso). Il nucleo ha già la
terza, `_member` — la guardia del login che rimanda a `/auth/login?returnUrl=…` e poi indietro — e ci stanno `/me` e `/contact`.

**Deciso** (caso b): `RouteDefinition.area` accetta `'member'`, e `createHubRouter` appende quelle rotte a `_member` come fa con le
altre due. Quindici righe, una frase in `FORKING.md`.

- **Scartato**: una pagina pubblica che controlla da sé `bootstrap.user` e fa il redirect. È la guardia di `_member` riscritta in
  un modulo, e il primo modulo che la dimenticasse mostrerebbe un form che risponde 401.
- **Scartato**: il form come dialogo sopra la pagina pubblica (già scartato in `il-pirep` §4).

## 2. Le procedure: il form le offre tutte e tre, il server dice quali mancano

Quali fra SID, STAR e IAP sono obbligatorie dipende dalle regole di volo del **piano al decollo** (§3.2 punto 4: `I`, `Y`, `Z`,
`V`), e il browser non lo sa: la lista delle sessioni (`TrackerSessionDto`) porta callsign, aeroporti, aereo e orari, non il piano.

**Deciso:** il form mostra sempre i tre campi, con una riga che spiega la regola quando il tour le chiede; il server rifiuta sul
campo che manca (`reportProcedureRequired`), e la riga arriva sotto quel campo.

- **Scartato**: aggiungere le regole di volo alla lista. Vuol dire leggere le revisioni del piano di **ogni** sessione della lista
  per mostrarne una colonna — una chiamata a IVAO per riga, per un'informazione che serve solo alla sessione scelta.
- **Scartato**: un secondo verbo «anteprima del volo scelto». Un giro in più prima di ogni invio, per dire in anticipo quello che
  l'invio dice comunque, con le stesse parole.

## 3. Dove arriva un rifiuto

Il server rifiuta campo per campo, ma metà dei campi del `PirepWriteDto` non sono campi di un form: `tour`, `legId`,
`sessionIds`, `diversionIcao`, `diversionReason`. Il generatore dei form mette un errore sul campo con quel nome, e un campo che
non disegna non lo mostra a nessuno.

**Deciso:** la pagina divide il rifiuto (`splitRefusal`, pura, con test): i campi del form dei dettagli vanno al form, il resto
diventa un avviso sotto la scelta del volo, che è la metà della pagina di cui parlano. Nessuna regola ripetuta nel browser: il
browser non sa perché, sa solo dove.

## 4. La correzione ripropone i suoi voli

Un report «da modificare» rivendica la sua sessione, e la lista delle sessioni **nasconde le rivendicate** (§3.4): senza un
aiuto, chi deve solo aggiungere la STAR dimenticata non ritroverebbe il volo che ha già inviato. **Deciso:** la correzione mette in
testa alla lista i voli del report (`sessionsOf`), già scelti; il server li accetta perché li riconosce come del report corretto
(T11a). Può sceglierne un altro, e la sessione vecchia si libera.

## 5. La deviazione

Una casella «il volo è finito in un altro aeroporto»; sotto, l'aeroporto, il motivo e una riga (un `SchemaForm` che si applica
mentre si scrive, senza pulsante), e poi la seconda lista, cercata **dall'aeroporto di deviazione alla destinazione della leg** —
o, su un `Open`, alla destinazione del piano del primo volo.

## 6. Il banco rivola un volo registrato

I voli registrati in `tests/fixtures/ivao/` sono di giugno e nessuna finestra li raggiunge. **Deciso:** il giro e2e
(`web/e2e/full/replay.ts`) scrive accanto agli originali una **copia** di un volo registrato con ogni istante spostato a ieri,
sotto il VID del banco e con un identificativo di sessione suo (dieci cifre che iniziano con 9, ignorate da git), e la toglie alla
fine. `FixtureIvaoApiClient` la legge come legge gli altri file. Cambiano anche i due aeroporti (il banco ne conosce cinque); il
piano, la traccia e le revisioni restano quelli veri.

- **Scartato**: un orologio finto nel prodotto, o una finestra più larga nel banco. Sarebbe codice di prodotto piegato per un
  test, e la finestra è proprio una delle cose che il giro prova.
- **Scartato**: un tracker finto registrato nel contenitore del banco. Sarebbe un secondo `IIvaoApiClient`, e i file esistono già.

⚠️ **Un tour con un PIREP non si elimina mai**, nemmeno ritirato (§1.2.2): il server rifiuta (`tourHasReports`) e il giro lo **nasconde**. Corretto dopo la prima CI, che
aveva creduto alla descrizione del dialogo («uno con report si nasconde») e si era fermata sulla pulizia. Il giro lascia quindi **un tour
nascosto per ogni esecuzione** nel database del banco. Nascosto, non è su nessuna pagina pubblica e nella lista dello staff lo
trova solo chi cerca `bench-report-`.

## 7. Che cosa resta fuori, e di chi è

- **L'avanzamento sui riquadri di `/tours`** per il pilota (§8.1) è del blocco `flightops.myTours`, **T15**.
- «Contesta», «Richiedi chiarimenti», «Segnala un problema» sulla pagina del tour: **T14**.
- Gli ATC contattati e le esenzioni nel form: **T12**.
- Il «fatta quando» **in sviluppo**, con il login IVAO vero: il tracker è quello vero, quindi serve un volo di Carmine degli ultimi
  giorni. Non verificato qui; il banco lo prova con il volo registrato.

## 8. Che cosa si è toccato

- **Nucleo del frontend**: `shared/modules.ts` (`area: 'member'`), `app/router.ts`.
- **Modulo**: `screens/report.tsx` (la pagina), `screens/reporting.ts` (le funzioni pure), `screens/public.tsx` (colori,
  «Invia il report», stato delle leg, «I tuoi report» con «Ritira» e «Correggi»), `api.ts`, `schemas.ts`, `index.ts`, le lingue.
- **Test**: Vitest `reporting.test.ts`; smoke `e2e/tours-report.spec.ts` (API finta); giro `e2e/full/tours-report.spec.ts` con
  `replay.ts`.
- **Documenti**: `FORKING.md` (la terza area).
