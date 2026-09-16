# Gli award del nucleo e le preferenze dell'utente

**Data:** 16 settembre 2026 — fase T4b di M2
**Stato:** **decisa** (Carmine, 16 settembre 2026, quattro domande in apertura di T4b).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: tutto passa dai meccanismi che esistono — il motore CRUD, l'unico handler,
`IProjectable`, i grant — e si estendono due contratti di poco (`AwardSignalProjection` e `IModule`).

## 1. Che cosa serviva

Il piano §9.1 descrive gli award come «catalogo (nome, immagine, dipartimento, criterio) + assegnazioni per VID con motivazione
e audit; permesso `Awards.Assign` per dipartimento/grant», e §9.7 dà la tabella `awards` una colonna `owner_department`. Ma
`Awards.Assign` è nel catalogo dei permessi come **globale** da M0, e la frase «per dipartimento/grant» non dice chi scrive il
catalogo. Il design M2 §11 (estensioni 7 e 15) e la fase T4 non lo dicevano nemmeno. Quattro punti aperti, quattro risposte.

## 2. Le decisioni

1. **Il catalogo è del dipartimento.** `hub_awards` è `IOwnedByDepartment`, con `Awards.View` e `Awards.Edit` come ogni area
   (coordinator, assistant e advisor li hanno sul proprio dipartimento, come i link): il FOD scrive gli award dei suoi tour.
   **Assegnare resta `Awards.Assign`, globale** (in IT a MD e HQ con i grant, cioè configurazione e non codice), e dà la coda e il
   registro. Il catalogo è **letto da ogni dipartimento** (`ISharedForReading` su tutte le righe), perché chi assegna deve
   vedere gli award di tutti. *Scartato*: tutto dietro `Awards.Assign`, con il dipartimento solo come etichetta — meno codice,
   ma il FOD avrebbe dovuto chiedere a MD di creare l'award di ogni tour.
2. **La segnalazione propone l'award.** `AwardSignalProjection(Vid, Reason, AwardId?)` e la colonna nullable
   `cms_award_signals.award_id`: un tour ha un solo award, e «assegna» nella coda propone già quello giusto. È una proposta:
   chi assegna può sceglierne un altro. Una segnalazione ancora in attesa segue la riga (se l'award del tour cambia, cambia
   anche la proposta); una già gestita non si riscrive mai. *Scartato*: lasciarla com'era e far scegliere l'award a mano ogni
   volta.
3. **Nessuna mail** al membro quando riceve un award, in T4b.
4. **Il membro non vede mai i suoi award nell'hub**: li vede sul suo **profilo IVAO**, dove l'award va assegnato comunque.
   L'hub tiene il **registro** di chi ha assegnato che cosa e perché. Il form dell'assegnazione lo ricorda a chi assegna.

## 3. Come è fatto (nessun endpoint scritto a mano)

- **Tre risorse del motore CRUD**: `/api/awards` (dipartimentale), `/api/award-assignments` e `/api/award-signals` (globali,
  lette e scritte con `Awards.Assign`, la forma dei grant).
- **Assegnare da una riga della coda** è creare un'assegnazione con `signalId`: `CrudOptions.BeforeSave` controlla che la riga
  sia in attesa e riguardi lo stesso VID, e la segna **gestita nello stesso salvataggio**. Un indice unico su
  `hub_award_assignments.signal_id` chiude la corsa di due persone sulla stessa riga. `signalId` si legge solo alla creazione.
- **Scartare** è un `PUT` dello stato su `/api/award-signals/{id}` (`Pending` ↔ `Dismissed`, con chi e quando); `Handled` lo
  scrive solo un'assegnazione, e una riga gestita non torna indietro. Revocare un'assegnazione non rimette la riga in coda.
- **Un award ricevuto da qualcuno non si elimina**: si ritira (`is_active`). L'eliminazione passa da `CrudOptions.Delete` e
  risponde `errors.awards.assigned`; la FK `hub_award_assignments.award_id` è `Restrict` (stesso contesto, quindi ammessa).
- **L'immagine** di un award è un uso senza scadenza (`MediaUseProjection(mediaId, null)`, sorgente `core`, `award:{id}`).
- **I nomi degli award** nella coda e nel registro arrivano con `CrudOptions.ToListPage`, una query per pagina.

## 4. Le preferenze dell'utente

Come il design M2 §4.1 e l'estensione 15 dicevano, senza scelte aperte: `hub_user_preferences` (`vid`, `key`, `value_json`),
`GET`/`PUT /api/me/preferences/{key}`. Le chiavi le dichiarano i moduli in **`IModule.Preferences`** (`PreferenceDescriptor`
con la chiave e che cosa accetta, e `PreferenceDescriptor.OneOf` per il caso comune), composte in `PreferenceCatalog` come i
permessi: una chiave deve chiamarsi `<modulo>.<nome>`, dichiarata una volta sola, o l'app non parte. Una chiave sconosciuta o
un valore che il modulo non accetta sono un 400 sul campo; nessuna riga vuol dire «il membro non ha scelto» e il modulo usa il
suo default. Il nucleo non dichiara preferenze sue: lingua e notifiche restano dove sono.

## 5. Che cosa si tocca

Piano 0.82 (§9.1, §9.7, §16.4), design M2 §3.11, §11 e §12, fase T4b in `06-piano-implementazione-m2.md`. Codice: `Core/Awards/`,
`Core/Preferences/`, `IModule`, `AwardSignalProjection`, la migrazione del nucleo `AddAwardsAndUserPreferences`, le schermate
`/staff/awards`, `/staff/awards/queue`, `/staff/awards/assignments`.
