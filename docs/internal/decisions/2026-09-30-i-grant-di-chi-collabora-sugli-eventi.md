# I grant di chi collabora sugli eventi: il permesso, non il dipartimento

**Data:** 30 settembre 2026 — fase E2 di M4 (lo scheletro), PR #209
**Stato:** **Proposta** — la domanda è a Carmine con un commento sulla PR ([la domanda][q]); la risposta si registra qui con il
link. Fino ad allora **i nove grant di AOD, FOD e MD non entrano in `division.json`** (§3), e la fase E3a, che li prova, aspetta.
**Regola applicata:** `CLAUDE.md` §0 regola 3 (un test del maintainer che va rosso dice che il cambio è sbagliato, non il test) e
§5: la raccomandazione (§2, (b)) estende un meccanismo del nucleo, quindi è una PR del nucleo a sé, con la sua nota, prima del
codice del modulo che la usa (regola 6). Design `09-design-m4.md` §1.1, §6.2; nota `2026-09-29-chi-lavora-sugli-eventi` §2.3–§2.4.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209

## 1. Che cosa è venuto fuori scrivendo i grant

- **Il design** (§6.2) e la nota `chi-lavora-sugli-eventi` (§2.4) danno a chi collabora grant **a una posizione** con `scope: ED`,
  a tutti i livelli (coordinator, assistant, advisor): AOD `Events.View` e `EventAtc.*`, FOD `Events.View` ed `EventRoutes.*`, MD
  `Events.View` ed `EventReports.*`. Nove righe. L'idea (§1.1): l'ED è in ogni evento, quindi un permesso tenuto sull'ED raggiunge ogni
  evento **nella parte** di chi collabora, senza entrare nel «a cura di».
- **Nel nucleo un grant su un dipartimento fa entrare chi lo tiene in quel dipartimento, per tutto quello che vede.**
  `HubClaims.BuildIdentity` scrive un claim `dept` per ogni dipartimento di un permesso che viene da un grant (`HubClaims.cs:253–272`),
  e su quei claim si reggono il filtro globale (le righe `Visibility.Department`) e il filtro di dipartimento di **ogni lista
  generata** (`MapCrudExtensions.TryNarrowToDepartments`, `MapCrudExtensions.cs:436–501`), che guarda i dipartimenti del lettore e
  non quale permesso ci tiene. È la decisione del 6 settembre (nota `2026-09-06-autorizzare-su-un-pezzo-di-un-altro-dipartimento`,
  «Cosa allarga, detto ad alta voce»), presa per **un grant a una persona**. I grant a una posizione sono arrivati dopo (M2, T5, nota
  `2026-09-13-moduli-non-subordinati-ai-dipartimenti` §3.2) sulla stessa strada: il calcolatore scrive la stessa sorgente `grant:{id}`
  per tutti e due (`EffectivePermissionsCalculator.cs:169–186`), e nessuna nota ha detto che cosa vuol dire per un grant a una
  posizione di un altro dipartimento.
- **Fin qui non ce n'era nessuno**: i grant del seme sono del FOD sul FOD e del TD sul TD, il dipartimento della posizione; quelli al
  team di un FIR, con `firStaffScope: own`, portano il FIR e non entrano nei claim (A11a). I nove di E2 sarebbero **i primi fra due
  dipartimenti**: ogni coordinator, assistant e advisor di AOD, FOD e MD entrerebbe nell'ED.
- **Misurato** (30 settembre 2026, la suite d'integrazione intera con i 22 grant del design in `config/division.json`): **430 test,
  4 rossi**. Tre sono del maintainer e cadono per questo:
  - `SeveralDepartmentsTests.ARowOfAModuleIsInTheCareOfItsBaseDepartmentAndOfWhoeverCollaborates` (riga 81): l'assistente del FOD
    trova nella lista una riga di SOD ed ED che non è sua;
  - `SeveralDepartmentsTests.TheQueryFilterOfAModuleContextReadsTheSet` (riga 120): la stessa riga, attraverso il filtro globale;
  - `SearchEndpointTests.SearchRespectsVisibility` (riga 82): il coordinatore del FOD trova nella ricerca il link che l'ED tiene per
    sé (4 invece di 3).
  Il quarto era `ErasureTests`, senza ancora le righe `evt_` (nota `2026-09-30-le-colonne-degli-eventi-in-erasuretests`). Con i soli
  grant dell'ED e del team di un FIR la suite è verde (§3).
- **Che cos'altro allargherebbe**, letto nel codice e non provato da un test: il gruppo «Eventi» (ED) nella barra dello staff di chi
  collabora (`staffDestinations.ts`, `reachableDepartments`), con dashboard, calendario, categorie e contatti dell'ED; le righe dell'ED
  in **ogni lista del back office** dove chi collabora tiene il permesso di lettura sul proprio dipartimento — bozze di pagine, news e
  documenti, link, media, voci di calendario, categorie, e **la coda dei contatti dell'ED**, cioè i messaggi dei membri all'ED
  (oggetto e mittente nella lista, il dettaglio rifiutato dall'unico handler, `ContactsEndpoints.cs:37`); le righe
  `Visibility.Department` dell'ED nella ricerca e nelle pagine. Per una trentina di persone, per ogni evento a venire.

## 2. La domanda

Chi ha che cosa resta com'è deciso (design §6.2); la domanda è **fin dove arriva un grant a una posizione su un dipartimento che non è
il suo**.

- **(a) Com'è scritto, con l'allargamento.** I nove grant entrano in `division.json`; chi collabora entra nell'ED per quello che vede,
  secondo la regola del 6 settembre. Nessun codice del nucleo. I tre test del maintainer vogliono un «esterno» che nessun grant porta
  nell'ED (per esempio un assistente del PRD): li cambia il maintainer.
- **(b) Un grant a una posizione su un dipartimento che non è il suo dà il permesso, non il dipartimento** — *raccomandata*. È la
  forma che A11a ha dato al team di un FIR («the team of a FIR is not part of the department, and sees the rows of its FIR through the
  permission itself», `HubClaims.cs:250–252`), per chi collabora:
  1. il permesso effettivo sa di venire da un grant a una posizione di un altro dipartimento (come sa il suo FIR,
     `EffectivePermission.Fir`), e il claim lo porta;
  2. `HubClaims.BuildIdentity` lascia quei dipartimenti fuori dai claim `dept`, come un permesso tenuto su un FIR;
  3. la lista generata aggiunge le righe dei dipartimenti su cui il lettore tiene **il permesso di lettura di quella lista** per
     quella via — la forma di `onTheirFir` in `TryNarrowToDepartments`, con un dipartimento al posto del FIR;
  4. l'unico handler non cambia (`Has(permission, department)` risponde già per permesso e dipartimento), né il guardiano
     dell'interceptor, né il filtro globale (nessuna riga degli eventi è `Visibility.Department`).
  Un grant **a una persona** resta come deciso il 6 settembre. Chi collabora vede gli eventi (`Events.View`) e la sua parte (rotte,
  ATC, PIREP di supporto) e nient'altro dell'ED; i tre test del maintainer restano verdi come sono. Vale anche per il team di un FIR
  con `firStaffScope: all` (una posizione senza dipartimento), che oggi entra nel dipartimento del grant: con `own` non ci entra già.
  **Costa una fase del nucleo** con la sua nota, prima di E3a: la chiamerei **E2b** («Nucleo: chi collabora non entra nel
  dipartimento»), dopo E2 e prima di E3a, che prova «FOD e AOD non modificano il testo» e «chi collabora non elimina».
- *(scartata)* **Chi collabora nel «a cura di» di ogni evento, da solo** (una lista di dipartimenti accanto a `baseDepartment`, grant
  sul proprio dipartimento): la nota `chi-lavora-sugli-eventi` §3 l'ha già scartata, e cambierebbe `ModuleBaseDepartment` da un
  dipartimento a un insieme — nucleo anche lei, e con un «a cura di» che non dice più chi cura l'evento.

**Raccomandazione: (b).** Il design dice «nella loro parte» (§1.1, §6.2), e la nota `chi-lavora-sugli-eventi` ha scartato il «a cura
di» proprio perché «darebbe loro tutto l'evento, non la loro parte»: (a) darebbe loro tutto l'ED. La regola del 6 settembre è stata
scritta per una persona che aiuta un dipartimento, non per tre dipartimenti interi; e la coda dei contatti dell'ED è fatta di messaggi
di membri. (b) è la stessa regola che il nucleo ha già per il team di un FIR, scritta una volta.

## 3. Che cosa fa E2 intanto

- **In `config/division.json` e `config/division.example.json`** ci sono gli **11 grant dell'ED** (EC ed EAC tutto tranne
  `EventReports.Edit`, EA1–9 senza `Events.Delete` né `Events.ManageSettings`) e i **2 del team di un FIR** su `EventAtc.*`, che con
  `own` non entrano nei claim e comunque aspettano la prima riga `IHasFir` dell'area (E11a: fino ad allora il seme li salta con un
  avviso e non li ricorda). Suite d'integrazione intera verde così (la PR dice le corse).
- **I nove di chi collabora non ci sono**: un grant del seme si applica una volta e non si toglie più togliendolo dal file (nota
  `chi-lavora-sugli-eventi`, «Da portare nel piano»), quindi non entra prima della risposta. `EventsArchitectureTests` li elenca come
  in attesa di questa nota e li rifiuta nei due file finché non passano nella tabella del design.
- **Con (a)** i nove entrano con una riga ciascuno, nella fase che segue la risposta (o in E2, se la risposta arriva durante la
  revisione), e il maintainer cambia i suoi tre test. **Con (b)** entrano con la fase E2b del nucleo, o subito dopo, prima di E3a.

## Da portare nel piano

- **Con (a)**: §6.3 del piano (i grant a una posizione): un grant a una posizione su un altro dipartimento fa entrare in quel
  dipartimento, per ciò che vede, **ogni** titolare della posizione — detto ad alta voce come il 6 settembre.
- **Con (b)**: §6.3 e §16.2–§16.3 (i grant, l'unico handler, la lista generata): un grant a una posizione su un dipartimento che non è
  il suo dà il permesso sulle righe di quel dipartimento, non il dipartimento; la nota del 6 settembre vale per i grant a una persona.
  In `10-piano-implementazione-m4.md`, la fase del nucleo **E2b** fra E2 ed E3a.
- In tutti e due i casi: nessun cambio al design §6.2 (chi ha che cosa resta com'è).
