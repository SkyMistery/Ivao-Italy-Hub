# Completamento, validatori, piloti, ban (T15a)

**Data:** 23 settembre 2026 — fase T15a di M2
**Stato:** **decisa** (Carmine, 23 settembre 2026, quattro risposte in apertura, tutte come proposte; il resto è design già scritto o
scelta tecnica dichiarata qui sotto)
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è nel design (`05-design-m2.md` §3.9, §3.11, §7.2, §7.3, §8.2,
§8.7). Qui le quattro scelte che il design lasciava aperte e **due estensioni piccole del nucleo** — un servizio che scrive un grant per
conto di un modulo, e un punto «dopo il salvataggio» del motore CRUD — con il perché nessun meccanismo esistente bastava.

## 1. Che cosa serve

Piano di implementazione, parte C, T15: il completamento con la segnalazione dell'award, le statistiche dei validatori con «aggiungi
validatore» e «togli», la pagina del pilota, i ban con la mail, il blocco `flightops.myTours`. **Fatta quando**: un tour di prova
completato compare nella coda degli award, e chi ha `Awards.Assign` lo assegna.

## 2. Le quattro risposte di Carmine

| Domanda | Risposta |
|---|---|
| T15 in una PR? | **Divisa come T11, T13 e T14**: **T15a il server** (completamento e segnalazione, grant e statistiche dei validatori, dati della pagina del pilota, ban e mail), **T15b le pagine** (validatori, pilota, ban, il blocco `myTours` nelle due metà, l'avanzamento sui riquadri) e il giro e2e «completato → award assegnato», che è il «fatta quando». |
| Su che cosa si abilita un validatore, visto che il PIREP di un sottotour ha lo scope del sottotour e i sottotour non compaiono nella lista dei tour? | **Sul tour di primo livello.** Il PIREP di un sottotour prende lo scope del `Container`: abilitato sul padre, un validatore valida anche i sottotour, pure quelli aggiunti dopo. In più **«tutti i tour»**, un grant senza scope. Scartati: tour per tour con i sottotour nel selettore (abilitare il padre non coprirebbe i figli); primo livello senza «tutti» (costringerebbe a un grant per tour chi valida tutto senza una posizione FOD). |
| Come arriva `Tours.ViewPilots` al validatore abilitato (§7.2, risposta 17)? | **Un secondo grant scritto insieme**: «aggiungi validatore» scrive anche `Tours.ViewPilots` sul dipartimento del modulo se il membro non ne ha già uno suo; «togli» l'ultimo tour lo toglie, **solo se l'aveva scritto «aggiungi validatore»**. Solo dati, visibili nella schermata dei permessi. Scartati: un'implicazione fra permessi nel catalogo (`Validate ⇒ ViewPilots`, un meccanismo nuovo del nucleo per un caso solo, caso c); togliere la pagina ai validatori (correggere il design contro la risposta 17). |
| Quali ore nel riepilogo di `myTours` («ore stimate» nel design §8.2)? | **Le ore volate davvero**: dal decollo all'atterraggio che il tracker ha registrato su ogni volo dei PIREP **accettati** (`fo_pirep_flights.takeoff_at`/`landing_at`, già salvati). Scartate: le ore stimate delle leg (un numero che nessuno ha volato) e nessuna ora. |

## 3. Com'è fatto nel codice

### 3.1 Il completamento e la segnalazione dell'award

- **Una risposta sola a «dove sta questo pilota in questo tour»**: `PilotProgress` (`Pireps/`), che raccoglie ciò che le regole pure
  chiedono (`TourRules`, `OpenRules`) e aggiunge il tipo che da sole non sanno: un `Container` è finito quando `required_subtours` suoi
  sottotour lo sono, letto dalle iscrizioni dei sottotour. Dà anche **una misura** — fatti su quanti, in leg, miglia, obiettivo o
  sottotour — che la pagina del pilota e `myTours` mostrano qualunque sia il tipo. `PirepSubmission.MineAsync` (la pagina del pilota di
  T11b) ora la usa invece di ripetere il calcolo, e il calcolo dei «fatti» di un `Open` (paesi e regioni degli aeroporti toccati) ci si
  è spostato.
- **`TourCompletion`**: chiamato dalla decisione **prima** del salvataggio che accetta, con il PIREP com'è in memoria. Se il tour è
  finito, l'iscrizione scrive `completed_at`; se è un sottotour e con questo il contenitore raggiunge `required_subtours`, completa
  anche il contenitore.
- **La segnalazione è una proiezione dell'iscrizione**: `Enrolment` diventa `IProjectable` (`flightops`, `enrolment:{id}`) e proietta
  un'`AwardSignalProjection` con il motivo e l'award proposto dal tour, consegnati da `TourCompletion` in una proprietà **non mappata**
  (`AwardSignal`), come l'oggetto del filo di una contestazione in T14b. Così PIREP accettato, iscrizione completata e segnalazione
  stanno **in una transazione**. Un sottotour completato non segnala niente di suo.
- **Mai tolto**: una leg aggiunta dopo, o la decisione riaperta e cambiata in rifiuto, lasciano `completed_at` e la segnalazione dov'è.
  L'iscrizione completata non si salva più: un salvataggio senza la proprietà non proietterebbe niente, e lo scrittore delle proiezioni
  toglie una segnalazione **ancora in attesa** che la sua riga smette di proiettare — scritto sulla proprietà.
- **Il motivo** è scritto nella lingua della divisione (`awards.tourCompleted`, «Ha completato il tour "…"»): la coda degli award lo
  mostra com'è, ed è la lingua che lo staff condivide.

### 3.2 Lo scope di un PIREP e i validatori

- **`fo_pireps.scope_tour_id`**: il tour su cui un validatore è abilitato a prendere il PIREP, il suo o quello del contenitore; scritto
  al primo invio, `ResourceScope` lo legge. La migrazione `AddValidatorScope` la riempie per i PIREP già inviati dal `parent_tour_id`
  dei loro tour. Nient'altro cambia: ogni «chi può validare questo PIREP» era già la domanda all'unico handler sulla riga.
- **`ModuleGrants`** (nucleo, `Core/Auth/`): scrive e toglie un grant a una persona per conto di un modulo che conosce le righe a cui
  darlo, **con le stesse regole** della schermata dei permessi — un permesso del catalogo, mai globale, solo a chi è staff — e sulla
  **stessa riga** `hub_user_grants`, quindi audit, sospensione quando si lascia lo staff e sessione vengono gratis (design §7.3).
  **Perché serve**: la schermata dei permessi non scrive mai uno scope (nota del 15 settembre), e `hub_user_grants` non ha un
  dipartimento da confrontare, quindi l'interceptor non la protegge: la protegge l'endpoint — `Permissions.Manage` per la schermata,
  `Tours.ManageValidators` per il modulo. Il prossimo modulo che abilita qualcuno su una riga (istruttori, eventi) usa questo.
- **`Validators`** (`People/`): `GET /api/flightops/validators?year=` (`Tours.ViewPilots`) — per ogni validatore accettati, rifiutati,
  da modificare nell'anno, i tour su cui è abilitato, «tutti i tour», sospeso; per ogni tour con decisioni nell'anno, lo stesso per
  validatore. **Si conta la decisione che un PIREP ha adesso** (`decided_by_vid`, `decided_at`): una decisione riaperta e ripresa è della
  seconda, un PIREP rimandato e reinviato non è di nessuno finché non si decide di nuovo. Elencati: chi ha un grant suo **e** chi ha
  deciso nell'anno (anche per posizione). `POST` e `DELETE /api/flightops/validators` (`Tours.ManageValidators`). Il motivo dei grant
  scritti qui è `flightops: tour validator`, che è anche come «togli» riconosce il `Tours.ViewPilots` suo.
- **«Subito»** vuol dire dal login dopo: un grant scritto cambia il timbro di sicurezza di chi lo riceve e il suo cookie non vale più
  (M0, «un permesso tolto morde alla richiesta dopo»), quindi un validatore già collegato riceve 401 e rientra con i permessi nuovi.
  Nessuna attesa della sincronizzazione; il test lo fissa.
- **La domanda aperta da T13b** («un validatore con il solo grant e senza posizioni staff non entra in `/staff`») **non si pone**: un
  grant a una persona si dà solo a chi è staff della divisione, che entra in `/staff`, e il menu conta un grant con scope (`HasAny`).
  Chi lascia lo staff perde insieme l'ingresso e i grant, sospesi.

### 3.3 La pagina del pilota e i ban

- **`GET /api/flightops/pilots/{vid}?year=`** (`Tours.ViewPilots`): errori confermati sui PIREP accettati e rifiutati — quelli che
  contano (T13a) — per categoria e per errore, nell'anno solare del decollo e da sempre, **con il nome che i PIREP hanno congelato** (un
  errore tolto dal catalogo si legge ancora); le leg volate con esito, chi ha deciso e la contestazione; i contatori delle
  contestazioni su tutti i tour; i fili (contestazioni e chiarimenti) **che chi guarda legge** nei contatti del suo dipartimento; i ban;
  i tour con la misura di `PilotProgress`; `canBan`.
- **I ban** (`/api/flightops/bans`): lista e form generati, `MapCrud`, lettura con `Tours.ViewPilots` (la pagina del pilota li mostra),
  scrittura con `Tours.Ban`; su un tour di primo livello o su tutti (un ban sul contenitore vale per i sottotour, T11); **mai
  cancellati** — si toglie un ban spostandone la fine, perché è parte del registro del pilota (§10.1). La mail **`flightops.banned`**
  al pilota, nella sua lingua, con motivo e durata, **quando il ban si scrive** e non quando si cambia.
- **`CrudOptions.AfterSave`** (nucleo): quello che segue un create o un update già salvato — mai un delete, mai una scrittura rifiutata —
  fuori dalla transazione della riga, come le mail dopo una decisione. **Perché serve**: il motore aveva solo i punti *prima* del
  salvataggio, e una mail accodata lì partirebbe anche per un ban che poi non si salva. Scartati: un endpoint di creazione scritto a
  mano accanto al motore (una lista generata con un «nuovo» fuori dal motore), la mail accodata in `BeforeSave`.
- ⚠️ La mail del ban si può spegnere dal profilo come ogni altra: nel nucleo ogni tipo di notifica è una preferenza del membro. Il ban
  vale comunque, e il form del PIREP lo dice (`flightops:errors.reportBanned`).

### 3.4 `myTours` passa a T15b

Il test del manifest vuole **le due metà di un blocco nella stessa PR** (un blocco che solo il server conosce disegna «blocco
sconosciuto»), e T13 aveva già messo `reviewQueue` intero nella fase delle pagine: `flightops.myTours` è **tutto di T15b**. Il contenuto
è deciso: i tour iniziati visibili al pilota (i nascosti spariscono, §1.2.2) con la misura di `PilotProgress` e la prossima leg, prima
quelli da finire; i PIREP da correggere; i fili (contestazioni e chiarimenti sui tour) con stato `Answered`; il riepilogo — leg
accettate, **minuti volati** dal tracker, tour completati di primo livello — senza contatori degli errori.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La segnalazione scritta dalla decisione nel contesto del nucleo | due salvataggi in due contesti: un award segnalato per un PIREP non accettato, o il contrario |
| La proiezione sul tour («chi l'ha completato») | il tour non si salva quando un pilota lo completa |
| Colonne di motivo e award sull'iscrizione | una copia del titolo e dell'award del tour, per un salvataggio solo |
| Scope del PIREP letto dal tour a ogni domanda | l'handler chiede lo scope sulla riga, anche in una lista di cento PIREP: una colonna scritta una volta non costa niente |
| Un grant `Tours.Validate` per sottotour scritto da «aggiungi validatore» | un sottotour aggiunto dopo resterebbe scoperto |
| Il modulo scrive in `hub_user_grants` da sé | le regole dei grant (catalogo, mai globale, solo staff) scritte una seconda volta |

## 5. Che cosa si tocca

- **Nucleo**: `Core/Auth/ModuleGrants.cs` (nuovo), `CrudOptions.AfterSave` e il motore che lo chiama.
- **Modulo**: `Pireps/PilotProgress.cs`, `Pireps/TourCompletion.cs`, `Enrolment` `IProjectable`, `Pirep.ScopeTourId`,
  `People/` (`Validators`, `Pilots`, `Bans`), `flightops.banned`, la decisione che completa, migrazione `AddValidatorScope` (una
  colonna riempita, un indice).
- **Test**: integrazione `PirepTests.People.cs` (VID 780089–780090, due formatori `IT-T87`/`IT-T88` di un altro dipartimento).
- **Piano** 0.98; **design** §1.8, §1.9, §3.9, §3.11, §7.2, §7.3, §8.2, §8.7; **piano di implementazione** T15.

## 6. Non verificato

- **Il tipo `Open`** completato non ha un test d'integrazione suo: passa dalla stessa strada (`PilotProgress` → `OpenRules.Progress`,
  provato dai test unitari di `PirepRulesTests`). Coperti da integrazione: `Sequential`, `Distance` in un sottotour, `Container`.
- **La coda degli award** con la segnalazione di un tour è provata dal database, non dalla schermata: è il giro e2e di T15b.
- **Le statistiche con molti anni di PIREP**: la query raggruppa sul database e c'è l'indice `(decided_by_vid, decided_at)`, ma nessuno
  l'ha misurata con numeri veri.
