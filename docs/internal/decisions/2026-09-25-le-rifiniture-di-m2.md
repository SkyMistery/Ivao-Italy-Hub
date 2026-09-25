# Le rifiniture di M2: i componenti di un modulo, il calendario di un tour, i PIREP fuori dalla ricerca

**Data:** 25 settembre 2026 — T20c (`06-piano-implementazione-m2.md`, T20 punto 3)
**Stato:** decisa da Carmine in chat il 25 settembre 2026, le quattro raccomandazioni
**Caso:** (b) per la galleria — si estende il manifest di un modulo, un meccanismo che c'è —; (a) per il calendario — un dato; il
resto è lettura del piano

## 1. Che cosa serviva

T20 punto 3 elenca le rifiniture di M2. Tre di esse chiedevano una risposta prima del codice:

- **`LegGrid` nella galleria.** Il piano (§8.3) lo mette fra i tre componenti di M2, ma `LegGrid` vive nel modulo, perché conosce le
  leg, e la galleria `/staff/admin/ui-kit` sta nel nucleo, che non importa da `modules/` (ESLint, design M0 §6.5). Il piano lo diceva
  apertamente: «come un modulo ci porta i suoi componenti lo dice T20».
- **Le due voci di calendario di un tour** (design §9, risposta 20), rilascio e chiusura, erano **identiche tranne l'ora**: stesso
  tipo `tour`, stesso titolo, stesso riassunto. Nel calendario non si capiva quale delle due fosse la chiusura.
- **«Il riepilogo dei PIREP nella ricerca e nel calendario rivisto»**, la riga di T20c scritta in T0. Il codice non proietta niente di
  un PIREP (`Pirep.Project`: solo il filo di una contestazione), e il design §9 nomina solo il tour.

## 2. Le domande e le risposte

**(1) Come entra `LegGrid` nella galleria?**

- **A — dal manifest** (raccomandata): `ModuleManifest` guadagna `components`, un elenco di `{ name, sample }`. Il registry li compone
  come compone i blocchi, e la galleria li mostra in una sezione «Componenti dei moduli», col nome del modulo davanti come un tipo di
  blocco (`flightops.LegGrid`). Il campione è del modulo: il componente con dati d'esempio, mai una chiamata al server. L'elenco resta
  una decisione, perché il test della galleria ne scrive i nomi per esteso.
- B — resta fuori: `LegGrid` non è un componente dell'elenco chiuso ma la schermata dell'eccezione dichiarata (§16.6). Nessun codice,
  si corregge §8.3. Scartata: la galleria smette di essere il posto dove si guarda **ogni** pezzo in uso, che è la ragione per cui esiste
  (`CLAUDE.md` §4).

**Carmine: A.**

**(2) Come si distingue la chiusura di un tour nel calendario?**

- **A — il tipo `deadline`** (raccomandata): la voce della chiusura usa il tipo di calendario «deadline», che il seme del nucleo ha già
  (arancione, «Scadenza»); il rilascio resta `tour`. Solo dati.
- B — un'etichetta nella proiezione: `CalendarProjection` guadagna una chiave i18n facoltativa («Apre», «Chiude») che il calendario
  mostra accanto al titolo. Più precisa, ma è un'estensione del nucleo che tocca proiezione, tabella e `CalendarView` per una parola.
- C — lasciare così.

**Carmine: A.** ⚠️ **Una conseguenza** da sapere, e scritta anche sul codice (`Tour.CloseCalendarKind`): un blocco calendario che
mostra **solo** il tipo `tour` mostra solo i rilasci; per avere anche le chiusure si sceglie anche `deadline`. Nessun blocco del seme
filtra per `tour`, quindi oggi non cambia niente di pubblicato.

**(3) I PIREP nella ricerca e nel calendario?**

- **Fuori** (raccomandata): nessun PIREP in ricerca né in calendario. Un PIREP è del pilota e dei validatori, e il design non vuole un
  elenco pubblico di chi ha volato che cosa (§8.2, `myTours`: «ognuno vede i suoi»). La riga di T0 va letta come «rivedere le voci del
  tour», che è la domanda (2).

**Carmine: sì, fuori.** Un test d'unità lo fissa (`TourStateTests.AReportPutsNothingInSearchNorInTheCalendarEvenWhenDisputed`).

**(4) Da dove si conta M2 nel rapporto di chiusura?**

- **Dal merge della #70** (raccomandata), l'ultima PR `m1/*`: M2 comincia con la #71 (H1). Le code di M1 (G13–G20, #57–#70) restano di M1.
- Dal 9 settembre, l'ultimo aggiornamento della chiusura di M1: conterebbe in M2 quattordici PR di M1.

**Carmine: dalla #70.** Il rapporto è `2026-09-25-m2-review.md`.

## 3. Che cosa è stato toccato

- **Nucleo, front end**: `shared/modules.ts` (`ComponentRegistration`, `ModuleManifest.components`, facoltativo), `app/registry.ts`
  (`Registry.components`, con il modulo), `features/admin/uiKitSections.tsx` (`UI_KIT_MODULE_COMPONENTS`), `UiKitPage.tsx` (la
  sezione, solo se c'è qualcosa), `uiKit.test.ts` (i nomi per esteso: `['flightops.LegGrid']`), la chiave `uiKit.moduleComponents`.
- **Modulo**: `screens/LegGridSample.tsx` (tre leg d'esempio, una ritirata, in un `QueryClient` suo già pieno, sola lettura),
  `components` nel manifest, `gallery.retiredReason` nei file di lingua.
- **Calendario**: `Tour.ReleaseCalendarKind` (`tour`) e `Tour.CloseCalendarKind` (`deadline`) in `Tour.Project`; `TourStateTests` ne
  controlla i tipi, `tours.spec.ts` legge le due voci con i due tipi; il test del fork XX controlla che un fork nasca con entrambi i
  tipi, nella sua lingua. Corretto il commento di `ProjectionSnapshot.Calendar`, che diceva «una voce per finestra di leg».
- **La checklist del fork XX** (`ForkabilityXxDivisionTests`): in più, tutte le impostazioni dei tour serializzate senza niente di
  questa divisione, la pagina pubblica dei tour di un fork, i due tipi di calendario.
- **Documenti pubblici**: `docs/FORKING.md` (lo stato, i componenti di un modulo, una sezione sui tour: chi li gestisce, le due
  impostazioni che nominano paesi, la mappa, le fonti esterne con l'archivio ATC, l'agente del validatore), `docs/UI-GUIDELINES.md` §3
  («A module's own components», `LegGrid`, la barra d'avanzamento che non è un componente), `CLAUDE.md` §2 (il manifest).

⚠️ La riga di T20c dice «`FORKING.md` (modulo, mappa, **OpenAIP**, agente)»: i confini dei FIR non vengono più da OpenAIP ma da VATSpy
(nota `2026-09-16-i-confini-dei-fir`), e `FORKING.md` lo diceva già; la sezione nuova rimanda a quel paragrafo.

## Da portare nel piano

- §8.3: `LegGrid` è nella galleria dal manifest del modulo; un modulo porta i suoi componenti con `components`.
- §9.5 (calendario) e design M2 §9: la chiusura di un tour è una voce `deadline`; i PIREP non proiettano né ricerca né calendario.
- Changelog e numero di versione.
