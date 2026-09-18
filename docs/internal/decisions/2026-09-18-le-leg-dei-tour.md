# Le leg dei tour: T7 in due, le righe figlie che seguono il tour, l'editor a tabella

**Data:** 18 settembre 2026 — fase T7a di M2
**Stato:** **decisa** (Carmine, 18 settembre 2026, due domande in apertura di T7; il resto sono estensioni di meccanismi esistenti,
dette qui perché toccano il nucleo o correggono il design).
**Regola applicata:** `CLAUDE.md` §5: **(c)** per la divisione della fase e per chi decide su una riga figlia; **(b)** per
`IAirportDirectory`, `ConfirmDialog` e il resto (si estendono meccanismi, non si aggirano).

## 1. Che cosa serviva

Aprendo T7, due bivi:

1. **La fase era grande quanto T6**: le leg con l'editor a tabella, la GCD e il tempo stimato, il ritiro, e poi hub e rotazioni,
   sottotour, vincoli sul callsign, tour `Open` con obiettivi, filtri e regole di sequenza, e i controlli di «pronto» di ogni tipo.
2. **Chi modifica una leg** (e poi un hub, una rotazione, un vincolo). Il design §1.1 dice che le righe scritte dallo staff hanno la
   maschera dei dipartimenti; non diceva da dove una riga figlia la prende.

## 2. Le decisioni

1. **T7 si divide** (Carmine). **T7a** sono le leg: `fo_legs`, GCD e tempo stimato, eliminare-rinumerare, ritirare e ripristinare,
   `LegGrid`, gli aerei consentiti del tour (tipi e gruppi) nel form, `required_nm` dei tour `Distance`, i controlli di «pronto» che
   dipendono dalle leg per `Sequential`, `Free`, `SequentialChosenStart` e `Distance`. **T7b**, in una chat nuova: hub e rotazioni,
   sottotour e `Container`, vincoli sul callsign, tour `Open` (obiettivo, filtri, regole di sequenza) e i loro controlli di «pronto»;
   chiude il «fatta quando» di T7 (un tour `Hub` pronto). Le domande sui sottotour (date e indirizzo propri o del padre) si portano
   all'apertura di T7b. *Scartato*: T7 intera (una PR troppo grande da rivedere); server e schermate separati come T6a e T6b (nessuna
   delle due metà si prova da sola).
2. **Le righe figlie copiano la maschera del tour** (Carmine). Una leg — e in T7b un hub, una rotazione, un vincolo — implementa
   `IOwnedByDepartment` con `owner_department` e `owner_department_mask` **copiati dal tour a ogni scrittura**, e ricopiati quando
   cambiano quelli del tour (`TourSaving`). L'unico handler e la guardia dell'interceptor leggono la leg come il tour, e le liste
   generate di T7b passano dal motore senza casi speciali. *Scartato*: nessuna colonna di dipartimento e autorizzazione solo sul
   padre (le liste generate non passerebbero dal motore così com'è).

## 3. Che cosa si estende, e perché

- **`IAirportDirectory`** (`Core/Ivao/AirportDirectory.cs`) e **`/api/reference/airports`**: la stessa forma di
  `IAircraftTypeDirectory` (T5). Un modulo chiede «dove sono questi aeroporti?» per congelare le coordinate di una leg e misurarla, e
  «quali cominciano così?» per la cella che li propone; non legge `ref_ivao_airports` da sé («questo nomina IVAO?» resta nel nucleo).
- **`ConfirmDialog`** prende `onOpenChange` e `confirmDisabled`: togliere una leg è un'eliminazione o un ritiro e lo sa solo il
  server, quindi l'editor glielo chiede quando il dialogo si apre e tiene spenta la conferma finché la risposta — e il motivo che un
  ritiro chiede — non c'è. Lo stesso dialogo esteso, non un secondo.
- **`ITourReports.LegsWithReportsAsync`**: quali leg hanno report, accanto a «il tour ha report?». Risponde «nessuna» finché T11 non
  la sostituisce; i test la provano sostituendo la risposta, come in T6a.

## 4. Le scelte piccole, dette qui

- **Sei verbi scritti a mano**, contati (l'eccezione dichiarata di §16.6 vale anche per il server): leggere la griglia, aggiungere
  (in coda o dopo una leg, `?after=`), modificare, chiedere che cosa farebbe il togliere, togliere, ripristinare. **Ogni scrittura
  risponde con tutta la griglia rinumerata**: l'editor non indovina mai un numero. La risorsa è il **tour**: leggere chiede
  `Tours.View`, scrivere `Tours.Edit`, sul tour, dall'unico handler.
- **L'indice `(tour_id, number)` non è unico**, anche se il numero lo è: MariaDB controlla un indice unico riga per riga, e spostare
  i numeri dopo un inserimento collide a metà. Il server rinumera tutto il tour da 1 a ogni inserimento o eliminazione, e questo lo
  tiene unico. *Scartato*: numeri negativi temporanei in due salvataggi (due righe d'audit per ogni leg spostata).
- **Una leg di un tour pronto passa i controlli di «pronto»** con le leg come la scrittura le lascia, come ogni salvataggio di un tour
  pronto (T6a): togliere l'ultima leg, o romperne l'anello, è rifiutato.
- **Il ripristino** vale finché il tour non è «in chiusura»: in chiusura risulta già chiuso a tutti. Una rotazione torna intera come
  si ritira intera. La regola della rotazione è già nel server e il test la prova con `rotation_id` scritto a mano: le rotazioni sono
  di T7b.
- **Le piste degli aeroporti** si chiedono dopo ogni scrittura (`IRunwayDirectory.EnsureAsync`), e un errore di rete non rifiuta mai
  una leg: l'aeroporto resta senza piste e i controlli che le usano diranno «non disponibile».
- **Il tempo stimato** si calcola a ogni lettura e si mostra per leg e in totale solo con l'aereo di riferimento e il suo profilo.
- **Nella griglia** gli aerei della leg si scrivono come tipi («A320, A20N»); i gruppi di una leg il server li accetta e la griglia li
  conserva, ma non li offre ancora. **La cella di un aeroporto** propone gli aeroporti con un `datalist` del browser, non con il
  campo a proposte chiuse del generatore dei form (che non è esportato e vive dentro `SchemaForm`); il server rifiuta un codice che
  non conosce, sotto la cella.
- **`LegGrid` vive nel modulo**, non in `shared/ui`: conosce le leg, e il nucleo non conosce i moduli. Per questo non è ancora in
  `catalog.ts` né nella galleria `/staff/admin/ui-kit`, che non può importare da `modules/` (design M0 §6.5). La galleria dei
  componenti di un modulo è di **T20** (parte C), che dovrà dire come un modulo ci porta i suoi, come per i blocchi.

## 5. Correzioni al design trovate nel lavoro

- **§1.5, la tabella del tempo stimato**: a 2000 NM la colonna «5 % + 20 min» diceva 4 h 40; la formula dà **5 h 00**
  (60 × 2000 × 1,05 / 450 = 280 min, più 20). 4 h 40 era la sola parte proporzionale. Il test unitario usa i numeri giusti.
- **§1.4**: «ordine nel tour, unico per tour» resta vero, ma lo tiene il server e non l'indice (sopra).

## 6. Che cosa si tocca

Piano 0.85 (§8.3 `ConfirmDialog`, §16.6 l'eccezione del server delle leg); design M2 §1.1, §1.4, §1.5, §8.4, §14; `06` parte C
(T7 divisa in T7a e T7b). Codice: `Core/Ivao/AirportDirectory.cs`, `shared/ui/ConfirmDialog.tsx`; nel modulo `Legs/` (entità,
`GreatCircle`, `EstimatedTime`, `TourShape`, `LegBook`, endpoint), `TourRules` (`AllowedAircraftCheck`, i controlli sulle leg, la
maschera che segue il tour), i DTO del tour, la migrazione `AddLegs`, `screens/LegGrid.tsx` e la scheda «Leg».
