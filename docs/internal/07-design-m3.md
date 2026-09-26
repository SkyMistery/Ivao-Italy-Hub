# IVAO Division Hub — Design di M3 (il modulo Training)

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (versione 1.09).
> Ingresso: i **requisiti dello staff TD**, raccolti con `dalberone` a domande (§R). Sui fatti — come funziona il
> training oggi e che cosa lo staff vuole — risponde `dalberone`; **le scelte le decide Carmine**: nel testo sono segnate
> **⚖️**, e in §12 c'è ognuna con la raccomandazione e **la decisione di Carmine** (25 settembre 2026). Le fasi si scrivono in
> `08-piano-implementazione-m3.md` **dopo** l'approvazione di questo documento. Il modello è `05-design-m2.md`.

**Stato:** **deciso** il 25 settembre 2026. I requisiti sono chiusi con le conferme di `dalberone` (R.7); Carmine ha
deciso le 15 domande di §12 sulla PR #121 ([risposte][r1], [secondo giro][r2], [n.5][r3]). Due giri di revisione: il teorico
dichiarato dal trainee è uno scostamento dal piano (§0.6, §12 n.15), le regole dei rating di IVAO stanno nel nucleo
(§1.7, n.4), i GCA sono nel profilo IVAO (§0.2), il feed del calendario non è in M3 (§12 n.14). Allineato al piano 1.09:
la cancellazione dei dati di una persona usa il meccanismo del nucleo di T20b, e un ban in vigore resta con
`ErasureRequest.Keep` (§6.1). Nessun codice: il prossimo passo è
la fase A0 (§11).

---

## 0. Perimetro

### 0.1 Che cosa è «fatto»

M3 è fatta quando lo staff TD può **spegnere PATS** (`training.ivao.it`, piano §13) **tranne il feed del calendario dei
trainer**, che resta su PATS fino all'iCal del nucleo in M6 (§12 n.14):

- un membro chiede un training — o il mock exam concordato — per il **rating successivo** al suo, ATC o pilota, e l'hub
  applica da solo le regole: conferma del teorico, ore minime, una richiesta alla volta, attesa dopo l'ultimo training
  (§2.2);
- chi può accetta o rifiuta con un motivo, con il promemoria di controllare il teorico; chi può assegna un trainer con
  il rating adatto (§2.3, §2.4);
- il trainer propone le sue disponibilità con gli avvisi sui conflitti, il trainee sceglie, e la data si può cambiare a
  mano; il training entra nel **calendario unico** (§2.5, §5);
- dopo la sessione il trainer rischedula, segna il no-show o compila la scheda e **pubblica il report**, con «pronto per
  il mock exam», «pronto per l'esame» e l'attesa tolta se serve (§2.6, §2.7);
- TC e TAC configurano le **voci di valutazione** per rating e le **impostazioni** del modulo;
- lo staff inserisce gli **esami** nel calendario (§2.8);
- partono le mail di ogni passaggio (§5.2); trainer e staff vedono lo storico di tutti, il trainee il suo.

### 0.2 Fuori perimetro

- **L'esame vero** e la **verifica del teorico**: stanno su IVAO, e l'API non li espone (piano §14; misurato il 25
  settembre 2026: la documentazione pubblica dell'API non ha endpoint di training né di esami). **Rating e GCA** di un
  membro invece arrivano nel suo profilo (`/v2/users/me`: `rating`, `gcas`, misurato il 3 settembre 2026): l'hub legge i
  rating e **non** i GCA.
- **Group training, GCA, flight briefing** (funzioni del TDCenter di HQ, piano §2.3-ter): fuori da M3 (§12 n.8).
  Una funzione sui GCA sarebbe fattibile senza fonti nuove, leggendo `gcas` dal profilo.
- **Il feed del calendario per i trainer**: i trainer usano quello di PATS per Google Calendar (d4), e il feed iCal del
  nucleo resta in M6, con la domanda aperta del piano §15.9 (§12 n.14). **PATS resta acceso solo per quel feed** fino a
  M6.
- **Discord**: M6. **Online Day**: è degli eventi, M4 (il modulo lo legge dal calendario quando esiste, §2.5).
- **Import dello storico di PATS**: solo un archivio in sola lettura, e solo se otteniamo il significato dei codici
  (§12 n.6, §7).

### 0.3 Che cosa M3 non rimette in discussione

- **I meccanismi del nucleo** (`CLAUDE.md` §2, piano §16): `MapCrud`, lista e form generati, l'unico handler, il filtro
  globale, `IOwnedByDepartment` a insieme, i grant a una posizione e per riga, `IProjectable`, il servizio notifiche,
  l'unico `IIvaoApiClient`, i blocchi Data, le dashboard, le impostazioni dei moduli, la cancellazione dei dati di una
  persona (`IPersonalDataEraser`, T20b).
- **I moduli fuori dai dipartimenti**: sezione `/staff/training`, «a cura di» con il TD sempre presente
  (`division.json → modules.training.baseDepartment: TD`, già scritto).
- **Chi gestisce il modulo** (piano 0.77): coordinator e assistant del TD tutto, per grant a una posizione; HQ e web
  tutto per il nucleo (`RolePermissionMatrix.ReachesEveryDepartment`: DIR, ADIR, WM **e AWM**).

### 0.4 I nomi

| Che cosa | Nome |
|---|---|
| Chiave del modulo (`IModule.Key`, `division.json`, storia delle migrazioni) | `training` |
| Progetto .NET | `IvaoHub.Modules.Training` |
| Frontend | `web/src/modules/training/` |
| Prefisso delle tabelle | `trn_` |
| API | `/api/training/...` |
| Area dei permessi | `Training` (`Training.Approve`, `Training.Conduct`…) |
| Rotte pubbliche e dei membri | `/training`, `/training/...` (segmento riservato `training`) |
| Rotte dello staff | `/staff/training/...` |
| Namespace i18n | `training` |

La sitemap del piano (§8.2) scrive anche `/me/training`: `/me` è la dashboard del nucleo, fatta di blocchi Data, e
il modulo ci entra con il blocco `training.myTraining` (§4.3); le pagine del trainee stanno sotto `/training`.

### 0.5 Che cosa si prende da PATS e dal TDCenter, e che cosa no

| Si prende | Da | Dove qui |
|---|---|---|
| Una riga per training, dalla richiesta al report | PATS `trainings` | §1.2 |
| Voci modello della scheda per rating, con ordine e tipo di voto | PATS `master_tasks`, `master_sheets` | §1.4 |
| Voto e commento per voce, note del trainer e dello staff | PATS `tasks`, `trainer_note`, `staff_note` | §1.4, §2.7 |
| Più sessioni per un training (i «tentativi») | PATS `attempts` | §1.3 |
| Il mock exam come training marcato, e l'attesa tolta dal trainer | PATS `mock_exam`, `bypass_break` | §2.7, §2.8 |
| L'elenco pubblico dei training programmati **senza nomi** (postazione, rating, data e ora) | PATS vista `trainingslist` | §5.1 |
| Le postazioni su cui si fa training, per rating e FIR | PATS `facilities` (143 righe) → dall'API IVAO | §1.7 |
| Richieste, sessioni, disponibilità, calendario, storico | TDCenter di HQ (piano §2.3-ter) | §2 |
| Il ban di un trainee, con scadenza e revoca | PATS `trainee` (`ban`, `expire_on`, `removed_by`) | §2.9 |

| Non si prende | Perché |
|---|---|
| Tabelle doppie per i piloti (`trainingsP`, `sheetsP`, `tasksP`…) | un campo `kind` (ATC o pilota) |
| Codici numerici senza nome (`status` 0–7, `approved` 0–3) | stati con un nome (§2.1) |
| Impostazioni chiave/valore (`settings`) | le impostazioni dei moduli del nucleo (§1.6) |
| Il feed del calendario dei trainer (`GCalendar`, con il suo token) | resta su PATS fino all'iCal del nucleo in M6 (§12 n.14) |
| Email come dato del modulo | l'indirizzo è del nucleo (`hub_users.Email`), usato solo dalla coda delle mail |

### 0.6 Scostamenti dal piano

Dichiarati qui perché il revisore li trovi senza cercarli; ognuno ha la sua domanda in §12.

| Il piano dice | Il design fa | Perché | Dove |
|---|---|---|---|
| Gli esiti del teorico li inserisce **lo staff**, a mano o da CSV, dietro `ITheoryExamSource` (§9.2, §14) | il **trainee dichiara** di averlo superato; chi approva lo controlla su IVAO; la dichiarazione passa da `ITheoryExamSource`, che resta il punto dove un giorno entra l'API | è il requisito del TD (d1): oggi PATS fa così, e lo staff non ha gli esiti da inserire | §2.2; ⚖️ §12 n.15 |
| Le pagine del membro sotto `/me/training` (§8.2) | sotto `/training`; `/me` riceve il blocco `training.myTraining` | `/me` è la dashboard del nucleo, fatta di blocchi | §0.4 |
| M3 spegne `training.ivao.it` (§13) | PATS resta acceso **solo per il feed del calendario dei trainer**, fino all'iCal del nucleo in M6 | i trainer lo usano (d4), e il feed non entra in M3 | §12 n.14 |

---

## R. I requisiti raccolti

Ogni punto dice da dove viene: **(d1)**, **(d2)**, **(d3)** e **(d4)** sono le risposte di `dalberone` ai gruppi 1–4
del 25 settembre 2026, **(d3-form)** lo screenshot della richiesta di oggi in PATS che ha mandato con il gruppo 3;
**(PATS)** è ciò che si legge nel dump di PATS del 12 settembre 2026 (§P).

### R.1 Chi fa che cosa

- **Direttore, vice direttore e web master fanno tutto** (d1). Il nucleo lo fa già: `RolePermissionMatrix.ReachesEveryDepartment`
  dà ogni dipartimento a `Director` e `Web` di livello coordinator e assistant, quindi anche all'assistente del web master.
- **Approvano una richiesta**: HQ, TC, TAC, TA1–9 (d1). I trainer (T01–T99) no.
- **Assegnano il trainer**: HQ, TC, TAC e i **capi FIR** — CH e ACH — **solo per i training del loro FIR** (d1, d2).
- **Una richiesta accettata e non ancora assegnata è normale**: resta accettata finché qualcuno assegna (d1).
- **Chi può fare un training**: un trainer con rating ATC **uguale o superiore** a quello del training (ADC da ADC in
  su, APC da APC in su…); SEC, SAI e CAI fanno tutti i training (d2). Stessa logica per i piloti (d1). (Nel design è
  una sola regola, «uguale o superiore», sull'ordine dei rating del nucleo: §1.7.)
- **L'assegnazione non si rifiuta**: trainer e staff si accordano prima in privato (d2).
- **Chiunque fa training vede lo storico di tutti i training e quelli aperti** (d2).
- **Le note riservate allo staff le vedono anche i trainer** (d3); il trainee mai.

### R.2 La richiesta

- **Non si può verificare il teorico** fatto su ivao.aero (d1; piano §14: lo scope `training` non c'è).
  Al clic su «Richiedi training» compare la domanda «Hai superato l'esame teorico per *rating* su ivao.aero?».
  Con **no** la richiesta è **rifiutata in automatico** con il messaggio «Prima di richiedere il training devi superare
  l'esame *rating* su ivao.aero», e **resta registrata** (d1).
- **Chi approva ha un promemoria**: controllare su ivao.aero che il candidato abbia fatto l'esame teorico (d1).
- **Ore minime di connessione**, ATC o pilota, come soglie nelle impostazioni del modulo (d1). **Sotto la soglia la
  richiesta è bloccata in automatico** (d3).
- **Precompilati**: VID, nome e cognome, ore di connessione ATC o pilota secondo il training chiesto (d2).
- **Si chiede solo il rating successivo al proprio**: un AS3 chiede solo ADC, un ADC solo APC, e così via (d2).
- **La postazione** si sceglie scrivendo, da un elenco delle postazioni su cui si fa training (d2).
- **Un testo libero** per le disponibilità e uno per note e richieste (d2).
- **Una richiesta alla volta**; il trainee la **annulla** finché non è accettata (d2).
- **Training dei piloti**: esistono, stessa logica (d1): PP, SPP e CP. **Il pilota non sceglie niente**: la richiesta
  ha i dati precompilati, il tipo (training o mock exam) e un testo libero (d3, d3-form).
- **La richiesta di oggi in PATS** (d3-form): la nota «prima di richiedere un training devi aver superato il teorico per
  quel rating»; VID, nome ed email in sola lettura (l'email si cambia su IVAO); il badge del rating; il tipo, **Training**
  o **Mock Exam** (disabilitato finché non è concordato); un testo libero; il pulsante «Request Training».

### R.3 Le date

- Dopo l'assegnazione **il trainer inserisce le sue disponibilità** per quel training e **il trainee sceglie** quella che
  preferisce; le date gli si presentano **a blocchi**, in modo chiaro (d1, d2).
- **Avvisi, non blocchi**: se quel giorno ci sono altri training, un evento o un online day, il trainer è avvisato ma
  può confermare (d1). In futuro un'impostazione sceglie **blocca / avvisa / niente**, predefinito **avvisa** (d1).
- **Override a mano**: la data si cambia per un imprevisto, anche con una data che non era tra le disponibilità; se
  nessuna va bene, ci si accorda in privato e si usa l'override (d2).
- **Nessun tempo massimo per rispondere** di default, ma configurabile; dopo **X giorni** configurabili senza risposta
  del trainee, **avviso al trainer sulla dashboard** (d2).
- **Nessuna risposta, mai**: lo staff chiude la richiesta, che resta in memoria (d2).

### R.4 Gli stati di un training (d1, d2)

1. **In attesa di accettazione** — la richiesta è registrata.
2. **Accettata, in attesa del trainer**.
3. **Accettata, trainer assegnato**.
4. **Programmato** — c'è la data.
5. **Eseguito** — dal giorno dopo la data.
6. **Completato** — quando il trainer pubblica il report.

E le uscite: **rifiutata in automatico** (teorico non fatto), **rifiutata dallo staff** (con un motivo, per esempio le
ore minime), **annullata dal trainee** (prima dell'accettazione), **chiusa dallo staff** (nessuna risposta), **no-show**.
Tutte restano in memoria.

### R.5 La sessione, il report, l'attesa

- **Scheda di valutazione**: task e voti **configurabili da TC e TAC** (d1), nelle impostazioni delle voci da valutare
  (d3). Le **parti pratiche** hanno un **voto da 1 a 5**; le **voci di teoria** una spunta **fatto / non fatto / da
  migliorare** (d3).
- **Per ogni voce**: il voto e un commento **visibili al trainee**, e una **nota riservata allo staff** (d1).
- **Il report lo pubblica il trainer da solo** (d2). **Non c'è superato / non superato**, perché non è un esame:
  solo i voti, più le caselle **«pronto per il mock exam»** e **«pronto per l'esame»**, un commento generale per il
  trainee e uno riservato allo staff (d2).
- **Training andato male**: si chiude comunque, e il trainee chiede una nuova sessione (d1).
- **Attesa di 5 giorni** prima di chiedere il training successivo, **dopo ogni training**, andato bene o male (d1, d2);
  il trainer la toglie con una casella quando chiude il report (d1, d2).
- **Poco traffico**: la casella «Rischedula» manda nuove disponibilità per una seconda sessione. Nessun report finché
  il training non si chiude; lo staff scrive **appunti interni** sulla pagina di valutazione, mai visibili al trainee (d1).
- **No-show**: il training si chiude e si applica un'**attesa propria del no-show**, configurabile (d2).

### R.6 Mock exam ed esame

- **Il mock exam** è un training avanzato che **simula l'esame**: il trainer non dà suggerimenti, il candidato agisce da
  solo, poi c'è il debriefing. Completa il percorso di training (d1).
- **Non lo sceglie il trainee** (d3): il trainer spunta «pronto per il mock exam» nel report, e **resta tracciato sul
  percorso del trainee**, anche se poi cambia trainer. Alla richiesta successiva il trainee vede «questo sarà un mock exam,
  come concordato con il trainer» (il trainer lo dice nel debriefing). **La scheda di valutazione è la stessa** dei
  training (d3).
- **L'esame** si gestisce interamente su ivao.aero. All'hub serve **solo la voce nel calendario** (d1), che **lo staff
  inserisce a mano** (d3).

### R.6-bis Notifiche e calendario (d3)

- **Mail**: richiesta ricevuta; accettata o rifiutata; trainer assegnato; date proposte; data confermata; promemoria
  prima del training.
- **Il calendario pubblico** mostra di un training **VID, postazione, data e ora**; **i nomi solo a chi ha fatto il
  login**.

### R.6-ter Non noti (d3)

- Group training, GCA holders, flight briefing (le altre funzioni del TDCenter di HQ): `dalberone` non sa se servono.

### R.7 Le conferme del gruppo 4 (d4, 25 settembre 2026)

- **Promemoria** 24 ore prima.
- **Mail** anche quando il report è pubblicato e quando il training è chiuso per no-show o nessuna risposta; per il
  **rifiuto automatico** basta il messaggio a schermo.
- **Attesa dopo un no-show** configurabile, predefinita **14 giorni**; **avviso al trainer** dopo **3 giorni** senza
  scelta della data.
- **ATC e piloti sono due percorsi indipendenti**: una richiesta aperta **per percorso**, e l'attesa vale **per
  percorso**, con lo stesso numero di giorni.
- **Si assegna chiunque sia staff del training** — HQ, TC, TAC, TA e i trainer locali — **per piloti e ATC**: chi fa
  che cosa lo sa lo staff, l'hub non lo decide.
- **Una voce della scheda può restare senza voto**: «N/A».
- **Gli esami li mette in calendario chi ha l'esame assegnato** (su IVAO). *Precisato il 26 settembre 2026* (nota
  `2026-09-26-gli-esaminatori`): un esame si assegna solo a HQ, TC, TAC o a un TA, **mai a un trainer**.
- **Il ban di un trainee serve** (come in PATS); la forma la propone il design, e resta configurabile (§2.9).
- **I trainer usano il feed per Google Calendar** di PATS.

---

## P. PATS oggi — il dump del 12 settembre 2026

Il dump (phpMyAdmin 5.2.3, MariaDB 10.5.29) è stato fornito da `dalberone` e **non sta nel repository**: contiene dati
personali. Importato in locale il 25 settembre su MariaDB 11.4.10 **senza errori**. Qui solo struttura e numeri.

| Database | Dati | Contenuto | Stato |
|---|---|---|---|
| `trainingNEW` | mag 2020 → 12 set 2026 | 1.078 training ATC (`trainings`), 478 piloti (`trainingsP`), 8.590 task valutati, 5.170 righe di storico delle schede, i task modello per rating, 16 impostazioni | **vivo**: è PATS |
| `exam` | dic 2015 → set 2026 | 729 esami (`examList`: esaminatore, voto, esito, stato IVAO e di sistema); `examlistOLD` è una copia ferma ad aprile 2026 | **vivo** |
| `training` | dic 2014 → dic 2020 | 1.537 training ATC, 188 piloti, 991 schede a 20 voci fisse | fermo dal 2020 |

- `trainingNEW` è in utf8mb4 e pulito; **non ha tabelle di utenti**: le persone sono VID. `training` è latin1 e ha
  130 training con testo rovinato già nell'originale.
- I codici (`status` 0–7, `approved` 0–3, `type`, `outcome`…) hanno un significato che sta nel codice PHP di PATS, che
  non abbiamo.
- I rating dei training sono 5–7 (ADC, APC, ACC); quelli degli esami 5–8.

---

## 1. Il modello

Tutte le tabelle stanno in `TrainingDbContext : ModuleDbContext`, con la sua `__EFMigrationsHistory_training`. Nessuna
FK verso il nucleo: `vid` e i codici delle postazioni sono colonne non vincolate. I rating sono **numeri** IVAO (§1.7).

### 1.1 Chi possiede una riga

- Le righe dello staff (voci della scheda, esami) e il training sono **`IOwnedByDepartment`** con `OwnerDepartmentMask`
  (il TD c'è sempre), **`IAuditable`** e **`[Audited]`**, area `[PermissionArea("Training")]`.
- **Il training lo crea il trainee**: è **`ISubmittedByMembers`**, e **`IHasStakeholder`** con il VID del trainee, che
  quindi lo annulla e sceglie la data (l'eccezione del guardiano per chi modifica la propria riga) ma **non lo approva,
  non lo assegna e non lo conduce**, nemmeno se è staff (§3).
- **Niente `IHasParticipants`**, di proposito: l'handler darebbe al partecipante `Training.View` sulla riga, e con lui
  la risposta dello staff, note riservate comprese. Il trainee legge il suo training dai **suoi** endpoint, con un DTO
  senza note (come i PIREP di un pilota in M2); il trainer ha già `Training.View` per posizione.
- **`IHasFir`**: il FIR della postazione; vuoto per i piloti. Serve ai capi FIR (§3.2, estensione n.2).
- **`IHasResourceScope`**: `training:training:{id}`, per il trainer assegnato (§3.3).
- **`IVisible`**: `Members`, ristretto al trainee e a chi ha `Training.View`, come un PIREP (design M2 §1.1).
- **Le righe figlie** (disponibilità, sessioni, voti) sono **classi semplici** che si scrivono con il training, come
  `PirepError` e `PirepEvent` in M2: l'autorizzazione è quella del training.

### 1.2 Il training — `trn_trainings`

| Campo | Che cosa |
|---|---|
| `kind` | `Atc` o `Pilot` |
| `rating` | il rating che si allena: il successivo a quello del trainee, tra quelli che hanno un training pratico secondo il vocabolario del nucleo (§1.7, §2.2) |
| `is_mock_exam` | deciso dall'hub alla richiesta (§2.8), mai dal trainee |
| `position`, `airport_icao`, `fir` | solo ATC: la postazione scelta (`LIRF_TWR`), il suo aeroporto e il suo FIR |
| `trainee_vid` | lo stakeholder |
| `trainee_rating_at_request`, `trainee_hours_at_request` | fotografia al momento della richiesta |
| `theory_confirmed_at` | quando il trainee ha risposto «sì» alla domanda sul teorico |
| `availability_text`, `notes_text` | i due testi liberi della richiesta |
| `state` | §2.1 |
| `rejection` | `TheoryNotPassed` (automatico) o `Staff`, con `rejection_reason` |
| `decided_by`, `decided_at` | accettazione o rifiuto |
| `trainer_vid`, `assigned_by`, `assigned_at` | l'assegnazione |
| `scheduled_start_utc`, `chosen_slot_id` | la data della sessione in corso |
| `ready_for_mock_exam`, `ready_for_exam`, `cooldown_waived` | le caselle del report |
| `general_comment`, `staff_comment` | commento per il trainee e commento riservato |
| `completed_at`, `closed_by`, `closed_at`, `close_reason` | chiusura: report, no-show, nessuna risposta, annullamento |
| `row_version` | concorrenza ottimistica |

**«Eseguito» non si scrive**: è `Scheduled` con la data passata — dal giorno dopo, nel fuso della divisione — come lo
stato dei tour è derivato dalle date (design M2 §1.2). Nessun job.

### 1.3 Disponibilità e sessioni — `trn_slots`, `trn_sessions`

- **`trn_slots`**: le disponibilità del trainer per quel training (`starts_at_utc`, `ends_at_utc`), con gli **avvisi**
  calcolati quando le ha scritte (§2.5), in JSON. Sparite a sessione confermata o a training chiuso.
- **`trn_sessions`**: lo storico delle sessioni già passate: data, **esito** (`Held`, `Rescheduled`, `NoShow`) e gli
  **appunti interni** di una sessione rischedulata (R.5). La sessione in corso sta sul training (`scheduled_start_utc`);
  quando il trainer ne registra l'esito diventa una riga qui.

### 1.4 La scheda — `trn_sheet_items`, `trn_evaluations`

- **`trn_sheet_items`**: le voci modello, per `kind` e `rating`: sezione **pratica** (voto **1–5**) o **teoria**
  (**fatto / non fatto / da migliorare**), titolo **tradotto** (`Localized<string>`, tutte le lingue della divisione,
  ⚖️ §12 n.11), ordine, attiva. Una voce usata da un report **non si elimina**: si disattiva. Le scrivono TC e TAC
  (`Training.ManageSheets`), con lista e form generati.
- **`trn_evaluations`**: la scheda compilata di un training, una riga per voce: **fotografia** della voce (titolo e tipo
  di voto, come le regole congelate di M2 §5.4, così un report vecchio non cambia se la voce cambia), voto o spunta — o
  **N/A** se la sessione non l'ha toccata (d4) —, **commento per il trainee**, **nota riservata**. Il mock exam usa la
  stessa scheda (R.6).

### 1.5 Gli esami — `trn_exams`

Solo per il calendario (R.6): VID del candidato, **VID dell'esaminatore** (chi inserisce, d4), `kind`, `rating`,
postazione (ATC), data e ora. Lista e form generati; ogni riga proietta **una voce di calendario** di tipo `exam` (§5.1,
estensione n.6). Niente esito, niente voto: sono su IVAO.

### 1.5-bis I ban — `trn_bans`

VID del trainee, motivo, **fino a quando** (vuoto: finché qualcuno non lo toglie), chi l'ha dato, e chi e quando l'ha
tolto: la forma della tabella `trainee` di PATS. `IOwnedByDepartment` (TD), `[Audited]`, `IHasStakeholder` con il VID
bannato. Un ban vale per **entrambi i percorsi**. Lista e form generati (§2.9).

### 1.6 Le impostazioni — `TrainingSettings`

Impostazioni del modulo del nucleo (`ModuleSettingsDescriptor`, riga `modules.training.settings`), schermata generata,
permesso `Training.ManageSettings`. **I predefiniti non conoscono la divisione** (test «XX»).

| Impostazione | Predefinito | Fonte |
|---|---|---|
| `minimumHours` — ore minime per `kind` e rating chiesto | nessuna soglia | d1, d3 |
| `cooldownDays` — attesa dopo un training | 5 | d1, d2 |
| `noShowCooldownDays` — attesa dopo un no-show | 14 | d2, d4 |
| `maxResponseDays` — tempo per scegliere la data, poi chiusura | nessuno | d2; ⚖️ §12 n.9 |
| `responseReminderDays` — giorni senza scelta prima dell'avviso al trainer | 3 | d2, d4 |
| `conflictPolicy` — `Warn`, `Block`, `None` | `Warn` | d1 |
| `conflictKinds` — tipi di voce del calendario che avvisano | `["event"]` | d1; l'online day si aggiunge qui quando M4 ne crea il tipo |
| `reminderLeadHours` — anticipo del promemoria | 24 | d3, d4 |
| `hiddenPositions` — postazioni escluse dal training | vuoto | PATS `facilities` |
| `theoryExamUrl` — il sito dell'esame teorico, nel testo della domanda e nel promemoria | vuoto | d1; ⚖️ §12 n.12 |

### 1.7 Rating, ore e postazioni: dal nucleo

- **I rating** sono i numeri di IVAO, già salvati al login (`HubUser.RatingAtc`, `RatingPilot`; aggiornati solo a ogni
  login). Il modulo li legge da `HubDbContext`, come FlightOps (`PirepSubmission`). **Il modulo non scrive mai un numero
  di rating né una regola di IVAO**: l'ordine dei rating, quali hanno un **training pratico** (per IVAO oggi ADC, APC,
  ACC e PP, SPP, CP), **quale tipo di postazione** serve a ciascuno, la sigla e il nome sono regole e dati di IVAO, e
  stanno nel **vocabolario dei rating del perimetro IVAO del nucleo** (estensione n.4). Il modulo chiede al vocabolario
  «il rating successivo a questo, se ha un training», «questo rating è almeno quello?», «quali postazioni per questo
  rating». Anche «SEC, SAI e CAI fanno tutti i training» (d2) non è una regola a parte: segue dall'ordine, perché sono
  sopra ogni rating allenato.
- **Le ore di connessione** arrivano al login nel campo `hours` del profilo IVAO ma oggi non si leggono (estensione n.1).
- **Le postazioni ATC**: il nucleo non ne ha un elenco (solo FIR e aeroporti). L'API IVAO lo ha:
  `/v2/ATCPositions/all`, `/v2/airports/{airportId}/ATCPositions`, `/v2/subcenters/all` (documentazione pubblica, vista
  il 25 settembre 2026; i campi **si misurano** nella fase del nucleo). Estensione n.5, ⚖️ §12 n.5.

---

## 2. Il percorso

### 2.1 Gli stati

| Stato | Che cosa | Da qui |
|---|---|---|
| `Requested` | richiesta registrata | `Accepted`, `Rejected`, `Cancelled` |
| `Accepted` | accettata, senza trainer | `Assigned`, `Closed` |
| `Assigned` | trainer assegnato, data da fissare (anche dopo una rischedulazione) | `Scheduled`, `Closed` |
| `Scheduled` | data fissata; dal giorno dopo si mostra **Eseguito** | `Assigned` (rischedula), `Completed`, `NoShow`, `Closed` |
| `Completed` | report pubblicato | — |
| `Rejected` | automatico (teorico) o dallo staff, con motivo | — |
| `Cancelled` | annullata dal trainee prima dell'accettazione | — |
| `Closed` | chiusa dallo staff (nessuna risposta) o per il tempo massimo (§2.5) | — |
| `NoShow` | il trainee non si è presentato | — |

Nessuno stato si cancella: tutto resta in memoria (R.4). Chi può cosa sta in §3.

### 2.2 La richiesta

Pagina `/training/request`, solo con login. **Precompilati** e in sola lettura: VID, nome, rating, ore (d2, d3-form).
L'hub propone **un solo training**: il rating successivo a quello del trainee nel `kind` scelto (ATC o pilota), se il
vocabolario del nucleo gli dà un training pratico (§1.7) — con le regole di IVAO di oggi un AS3 vede «ADC», un ACC niente.
Il pilota non sceglie altro; l'ATC sceglie la **postazione** scrivendo, dall'elenco del §1.7: le postazioni che il
vocabolario lega a quel rating, meno `hiddenPositions`. Due testi liberi: disponibilità, note.

I controlli, in quest'ordine, **sul server** (i messaggi come `ProblemDetails`):

1. **Il ban**: un trainee bannato non chiede niente; il messaggio dice fino a quando (§2.9).
2. **Una richiesta alla volta per percorso**: nessun altro training aperto (`Requested` … `Scheduled`) del trainee **nello
   stesso `kind`**; ATC e piloti sono indipendenti (d2, d4).
3. **L'attesa, per percorso**: dall'ultimo training chiuso del trainee **in quel `kind`**, `cooldownDays` dopo un
   `Completed` (a meno di `cooldown_waived`), `noShowCooldownDays` dopo un `NoShow`; nessuna dopo un rifiuto, un
   annullamento o una chiusura (d4).
4. **Le ore**: sotto `minimumHours` la richiesta è **bloccata** (d3), con la soglia e le ore nel messaggio.
5. **Il mock exam**: deciso dall'hub (§2.8), mostrato come «questo sarà un mock exam, come concordato con il trainer».
6. **Il teorico**: al clic compare la domanda (d1), con il link di `theoryExamUrl`. **No** → la richiesta si **registra
   come `Rejected` con `TheoryNotPassed`** e il messaggio a schermo lo dice; **nessuna mail** (d4). **Sì** → `Requested`,
   `theory_confirmed_at`, mail `requestReceived`. ⚠️ **Scostamento dal piano** (§0.6): il piano vuole gli esiti inseriti
   dallo staff; qui la risposta la dà il trainee, attraverso `ITheoryExamSource` — l'implementazione di oggi è «chiedilo al
   trainee», e quando IVAO esporrà gli esiti la domanda sparirà senza toccare il flusso. ⚖️ §12 n.15.

Il trainee **annulla** una richiesta `Requested` (d2).

### 2.3 Accettare o rifiutare

Con `Training.Approve` (§3.2). La pagina del training mostra in evidenza il **promemoria**: «controlla che il candidato
abbia superato il teorico», con il link (d1). **Rifiuta** chiede un motivo, che va nella mail `requestRejected`.
**Accetta** porta a `Accepted` (mail `requestAccepted`).

### 2.4 Assegnare

Con `Training.Assign` — HQ, TC, TAC; i capi FIR sui training del loro FIR (estensione n.2). L'elenco dei trainer
proposti:

- **tutto lo staff del training** — HQ (DIR, ADIR), TC, TAC, TA e i trainer (T01–T99) — **per ATC e piloti** (d4): chi
  allena che cosa lo sa lo staff, e l'hub non lo registra; solo chi è entrato almeno una volta nell'hub (il roster è chi
  ha fatto login, piano §16.13);
- con il **rating** del `kind` del training **uguale o superiore** a quello allenato (d2), secondo l'ordine del
  vocabolario del nucleo (§1.7): chi sta sopra ogni rating allenato — per IVAO SEC, SAI, CAI e ATP, SFI, CFI — passa
  quindi sempre, senza una regola scritta nel modulo;
- mai il trainee stesso.

Il server ricontrolla il rating all'assegnazione. **Assegnare scrive il grant** che fa condurre al trainer quel training
(§3.3) e manda `trainerAssigned` a trainee e trainer. Riassegnare toglie il grant al trainer di prima. Un training resta
`Accepted` senza trainer quanto serve (d1).

### 2.5 Le date

- **Il trainer scrive le disponibilità** (`trn_slots`) con `Training.Conduct`. A ogni disponibilità l'hub calcola gli
  **avvisi**: altri training con una sessione quel giorno (qualunque trainer), e le voci del calendario unico di un tipo
  in `conflictKinds` che toccano quel giorno, lette dal calendario del nucleo in sola lettura come FlightOps legge
  `hub_users`. `conflictPolicy`: `Warn` mostra gli avvisi e chiede conferma, `Block` rifiuta, `None` non controlla.
  Mail `datesProposed` al trainee.
- **Il trainee sceglie** una disponibilità tra i riquadri (d1): il training passa a `Scheduled`, entra nel calendario,
  partono `dateConfirmed` a entrambi e le disponibilità spariscono.
- **Override** (d2): chi conduce il training — il trainer, o TC e TAC — scrive una data qualunque, anche fuori dalle
  disponibilità, con gli stessi avvisi; stessa mail.
- **Senza risposta**: dopo `responseReminderDays` il training compare in evidenza nel blocco del trainer (§4.3). Con
  `maxResponseDays` impostato, un job chiude i training senza scelta (`Closed`, ⚖️ §12 n.9). Lo staff chiude a mano
  quando vuole (`Training.Approve`), con un motivo.

### 2.6 Dopo la sessione

Dal giorno dopo la data il training si mostra **Eseguito**. Il trainer (o TC e TAC) ha tre strade:

- **Rischedula** (poco traffico, d1): la sessione diventa una riga `Rescheduled` di `trn_sessions` con i suoi
  **appunti interni**, il training torna `Assigned` e si propongono nuove disponibilità. Nessun report.
- **No-show** (d2): sessione `NoShow`, training `NoShow`, si applica `noShowCooldownDays`.
- **Report** (§2.7).

### 2.7 Il report

La scheda del §1.4, generata dalle voci attive di quel `kind` e rating: per voce voto o spunta, commento per il trainee,
nota riservata (d1, d3); poi commento generale, commento riservato, **«pronto per il mock exam»**, **«pronto per
l'esame»**, **«togli l'attesa»** (d2). **Pubblica** porta a `Completed`, scrive la sessione `Held` e manda
`reportPublished` (d4). Il trainer pubblica da solo (d2); nessun esito superato / non superato. Una voce che la sessione
non ha toccato resta **N/A** (d4).

### 2.8 Il mock exam e l'esame

- **Il mock exam non si sceglie** (d3): la richiesta successiva è un mock exam se l'ultimo training `Completed` del
  trainee per quel `kind` e rating ha **«pronto per il mock exam»** e non era già un mock exam. Lo leggono tutti i
  trainer e lo staff sul **percorso del trainee** (§4.2), anche se il trainer cambia. Stessa scheda, stesso flusso.
- **«Pronto per l'esame»** si vede sul percorso e nel blocco del trainee; l'esame si prenota su IVAO.
- **Gli esami nel calendario** (`trn_exams`): li inserisce **chi ha l'esame assegnato** su IVAO (d4). L'hub non sa chi è
  l'esaminatore, quindi `Training.ManageExams` va a chi può esaminare — TC, TAC e TA, **non i trainer** (nota
  `2026-09-26-gli-esaminatori`, che corregge «tutto lo staff del training») — (§3.2) e la riga registra chi l'ha
  scritta. Lista e form generati, voce di calendario pubblica (§5.1). Il guardiano chiede `Edit` per creare una riga
  dello staff: ⚖️ §12 n.10.

### 2.9 I ban

Con `Training.Ban` (TC, TAC; HQ e web per il nucleo), dalla pagina del percorso del trainee o dalla lista dei ban: un
motivo e, se si vuole, una scadenza (d4: «configurabile»). Il trainee riceve `banned` con il motivo e la scadenza; finché
dura non chiede training, e quelli già aperti vanno avanti (come i PIREP già inviati in M2, §15.2 n.10). **Togli ban**
registra chi e quando. I ban restano nello storico. Nessuno si banna da solo (negato all'interessato).

---

## 3. Permessi

### 3.1 Il catalogo

| Permesso | Che cosa permette | Negato all'interessato |
|---|---|---|
| `Training.View` | vedere nel back office tutti i training, aperti e storici, con note riservate e appunti | no (il nucleo non nega mai la lettura; vedi ⚖️ §12 n.13) |
| `Training.Approve` | accettare, rifiutare, chiudere una richiesta senza risposta | **sì** |
| `Training.Assign` | assegnare e cambiare il trainer | **sì** |
| `Training.Conduct` | disponibilità, override della data, rischedula, no-show, scheda e report | **sì** |
| `Training.Edit` | tutto su ogni training (TC, TAC); è anche il permesso che il guardiano chiede alle righe dello staff | **sì** |
| `Training.ManageSheets` | voci della scheda | no |
| `Training.ManageExams` | esami nel calendario | no |
| `Training.Ban` | bannare un trainee e togliere il ban | **sì** |
| `Training.ManageSettings` | impostazioni del modulo | no |

### 3.2 Chi li ha (`division.json → positionGrants`, `scope: TD`)

| | TC | TAC | TA1–9 | Trainer (T01–T99) | Capo FIR (CH, ACH) |
|---|---|---|---|---|---|
| `Training.View` | ✓ | ✓ | ✓ | ✓ (d2) | ✓ solo il suo FIR (estensione n.2) |
| `Training.Approve` | ✓ | ✓ | ✓ (d1) | | |
| `Training.Assign` | ✓ | ✓ | | | ✓ solo il suo FIR (d1, d2) |
| `Training.Conduct` | ✓ tutti | ✓ tutti | | ✓ **solo i training assegnati** (§3.3) | |
| `Training.Edit` | ✓ | ✓ | | | |
| `Training.ManageSheets` | ✓ | ✓ | | | |
| `Training.ManageExams` | ✓ | ✓ | ✓ | — (mai esaminatori: nota `2026-09-26-gli-esaminatori`) | |
| `Training.Ban` | ✓ | ✓ | | | |
| `Training.ManageSettings` | ✓ | ✓ | | | |

HQ (DIR, ADIR) e il web (WM, AWM) tutto per il nucleo; il superadmin tutto. Scritti in `config/division.json` e in
`division.example.json` nella fase dello scheletro, come M2 (nota `2026-09-16-impostazioni-dei-moduli`).

### 3.3 Il trainer conduce solo i suoi training

È il caso che il nucleo ha già deciso (`CLAUDE.md` §2: «un permesso su una riga sola»; nota
`2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`): **all'assegnazione** il modulo scrive con `ModuleGrants` un grant
`Training.Conduct` al trainer con lo scope del training (`training:training:{id}`), come «aggiungi validatore» in M2.
Il trainer resta quello che è — `Training.View` per posizione — più una riga.

Due conseguenze, dette prima:

- **Scrivere un grant chiede al titolare di rientrare** (cambia il suo security stamp, `CONTRIBUTING.md`): un trainer
  appena assegnato rifà il login alla richiesta successiva. In M2 capita di rado (si abilita un validatore); qui a ogni
  assegnazione.
- **I grant con scope viaggiano nel cookie**: non si lasciano accumulare. Un job notturno toglie quelli dei training
  chiusi (`Completed`, `NoShow`, `Closed`), così il secondo rientro cade di notte e non a report appena pubblicato.

**Deciso** (§12 n.1): il grant con scope, e il login in più a ogni assegnazione è accettato.

### 3.4 Più di un permesso scrive il training (estensione n.7)

Il guardiano dell'interceptor lascia scrivere una riga con `{Area}.Edit` oppure con **un** permesso alternativo
(`[AlsoWrittenWith]`, M2 T13), e **solo in modifica**: creare una riga dello staff chiede sempre `Edit`. Qui non basta
due volte:

- **sul training** scrivono tre ruoli senza `Edit`: chi approva (TA), chi assegna (capo FIR), chi conduce (trainer);
- **un esame** lo crea chi l'ha assegnato (d4), anche un TA, che non ha `Edit` (mai un trainer: nota `2026-09-26-gli-esaminatori`).

Il modulo **non si piega** a una tabella per ruolo (`CLAUDE.md` §5, caso b): si estende il meccanismo perché accetti
**più alternative** sulla stessa entità, e perché un'entità possa dire che la sua alternativa vale **anche alla
creazione** (senza scope: la riga non ne ha ancora uno). Ognuna chiesta come oggi: con lo scope della riga, mai
all'interessato, senza spostare la riga. Le righe figlie non ne hanno bisogno (§1.1).

---

## 4. Schermate e blocchi

### 4.1 Pubblico e membri

- **`/training`** (pubblico): i prossimi training e gli esami (il blocco `training.upcomingSessions`) e il pulsante
  «Richiedi training». Una pagina di contenuti che spiega il percorso resta del CMS, con lo stesso blocco.
- **`/training/sessions/{id}`** (pubblico): postazione, rating, data e ora; **VID e nomi** solo a chi ha fatto il login
  (R.6-bis; §12 n.4: niente VID ai visitatori). È l'indirizzo delle voci di calendario.
- **`/training/request`** (membri): la richiesta del §2.2, con la finestra della domanda sul teorico.
- **`/training/mine`** (membri): le mie richieste e i miei training, stato, attesa residua, «pronto per…».
- **`/training/mine/{id}`** (membri): scegliere la data **tra i riquadri** (d1); leggere il report — voti, commenti per
  il trainee, commento generale — **mai** note riservate né appunti: gli endpoint del trainee hanno un DTO loro.

### 4.2 Staff

- **`/staff/training`**: lista generata con i filtri **Da approvare**, **Da assegnare**, **In corso**, **Da chiudere**
  (eseguiti senza report), **Storico**; per un capo FIR solo il suo FIR.
- **`/staff/training/{id}`** (schermata dedicata, come la pagina di validazione di M2 §4.3): la richiesta con ore,
  rating e il promemoria del teorico; le azioni che chi guarda può fare (§3); le disponibilità con gli avvisi; lo storico
  delle sessioni con gli appunti; la scheda (form generato dalle voci); il report. Il **percorso del trainee** a fianco.
- **`/staff/training/trainees/{vid}`**: il percorso di un trainee — tutti i training per percorso e rating, «pronto
  per…», attesa, ban — come la pagina del pilota di M2 (§8.7); da qui «Banna».
- **`/staff/training/sheets`**, **`/staff/training/exams`**, **`/staff/training/bans`**, **`/staff/training/settings`**:
  liste e form generati.

### 4.3 Blocchi Data

| Blocco | Dove | Che cosa |
|---|---|---|
| `training.upcomingSessions` | `/training`, pagine | prossimi training ed esami, senza nomi per chi non ha fatto il login |
| `training.myTraining` | `/me` | per percorso: la richiesta aperta, la prossima data, «scegli la data», l'ultimo report, «pronto per…», l'attesa, un ban |
| `training.trainerQueue` | `/staff`, dashboard TD | i miei training da muovere: date da proporre, **in attesa di scelta da N giorni** (R.3), report da scrivere |
| `training.approvalQueue` | dashboard TD | richieste da approvare e da assegnare (per un capo FIR, del suo FIR) |

A un visitatore i blocchi personali rispondono `signedIn: false`, come `myTours`.

---

## 5. Calendario, notifiche, job

### 5.1 Calendario e ricerca

- **Una voce per sessione**: il training proietta la sessione in corso e le sessioni `Held` (più voci per riga,
  estensione n.10 di M2, già fatta), tipo `training`, visibilità `Public`, indirizzo `/training/sessions/{id}`. Titolo
  senza nomi e senza VID: rating e postazione (§12 n.4). Una sessione rischedulata o un no-show non restano.
- **Gli esami**: una voce di tipo `exam`, pubblica. ⚠️ Il tipo `exam` è nel piano (§7) ma non nel seme dei tipi
  (`seed/calendar-kinds/kinds.json`: `event`, `training`, `tour`, `meeting`, `deadline`): estensione n.6.
- **Ricerca**: niente. Un training non è un contenuto da cercare.

### 5.2 Notifiche (`training.*`)

| Tipo | A chi | Quando |
|---|---|---|
| `requestReceived` | trainee | richiesta registrata |
| `requestAccepted` | trainee | accettata |
| `requestRejected` | trainee | rifiutata dallo staff, con il motivo |
| `trainerAssigned` | trainee e trainer | assegnato o cambiato |
| `datesProposed` | trainee | nuove disponibilità |
| `dateConfirmed` | trainee e trainer | data scelta o cambiata a mano |
| `reminder` | trainee e trainer | `reminderLeadHours` prima (24 ore) |
| `reportPublished` | trainee | report pubblicato (d4) |
| `trainingClosed` | trainee | chiuso per nessuna risposta o no-show (d4) |
| `banned` | trainee | ban dato, con motivo e scadenza |

Il rifiuto automatico non manda mail: basta il messaggio a schermo (d4).

Modelli in `web/src/modules/training/locales/{it,en}/training.json`, copiati da `pnpm i18n:sync`; ogni membro le spegne
dal profilo (preferenze del nucleo).

### 5.3 Job del modulo

- **`training-reminders`**, ogni 15 minuti: le sessioni che iniziano entro `reminderLeadHours` e non hanno ancora il
  promemoria; una colonna `reminded_at` lo fa partire una volta sola (il nucleo non programma mail nel futuro).
- **`training-expiry`**, ogni notte: chiude i training oltre `maxResponseDays`, se impostato; toglie i grant con scope
  dei training chiusi (§3.3).

Convenzioni di M2: `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`, mai un'eccezione, `RunAsync` per i test.

---

## 6. Conservazione e dati personali

- **Il registro dei training resta**: richieste, stati, sessioni, schede, report e **ban** sono la storia del percorso
  di un membro, e il TD ci torna anni dopo (PATS li tiene dal 2014). Vanno via solo le **disponibilità** a sessione
  decisa.
- **Che cosa è personale**: il VID di trainee e trainer, i testi liberi della richiesta, commenti e note. Nome ed email
  non si copiano: si leggono dal nucleo.

### 6.1 La cancellazione dei dati di una persona

**Il meccanismo è del nucleo e c'è** (T20b, piano 1.08, nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`):
il superadmin cancella dal pannello dei permessi; il nucleo sostituisce da solo, in ogni contesto, il VID nelle colonne
che si chiamano `vid`, `*_vid` o `*_by` con uno **pseudonimo negativo**; ogni modulo che tiene dati di persone implementa
**`IPersonalDataEraser`** (anteprima ed esecuzione) per ciò che è **sulla** persona. Il modulo si aggancia, non scrive un
meccanismo suo.

- **Le colonne seguono la convenzione**: `trainee_vid`, `trainer_vid`, `examiner_vid`, `decided_by`, `assigned_by`,
  `closed_by`, il `vid` e i `*_by` dei ban. Nessuna lista di VID in JSON. Il test che elenca le colonne di persona
  (`ErasureTests`) le vedrà da solo.
- **`TrainingPersonalData : IPersonalDataEraser`**, **deciso** (§12 n.7), con le quattro risposte di Carmine della nota
  come regola:
  - i **training chiusi** del trainee (`Completed`, `NoShow`, `Closed`, `Rejected`, `Cancelled`) sono **il registro**:
    restano, con lo pseudonimo; vanno via **tutti i testi liberi** che parlano di lui — i due della richiesta, i commenti
    per il trainee, le note riservate, i commenti del report, gli appunti delle sessioni, il motivo di un rifiuto —;
    restano stati, date, rating, voti e spunte;
  - i **training aperti** (`Requested` … `Scheduled`) si **cancellano**: non vanno avanti senza la persona (come i PIREP
    aperti);
  - gli **esami** in cui è candidato si cancellano (sono voci di calendario, l'esame è su IVAO);
  - un **ban in vigore resta** con VID e motivo, con `ErasureRequest.Keep(ban)` (piano 1.09, nota
    `2026-09-25-le-righe-che-restano-con-il-vid`: senza, il nucleo lo renderebbe anonimo come ogni colonna `vid`); uno
    scaduto si anonimizza (risposta 2 della nota);
  - ciò che ha fatto **come staff o trainer** — training condotti, decisioni, assegnazioni, esami inseriti — resta con lo
    pseudonimo, e i suoi testi restano perché parlano di altri (risposta 4).
- **«Persona cancellata»**: dove il modulo mostra un VID (percorso del trainee, liste, pagina della sessione), un VID
  negativo diventa «persona cancellata» senza link. La nota dice che l'helper passa nel nucleo quando Training ne ha
  bisogno: è questo il momento (n.10).

---

## 7. Lo storico di PATS

Il dump del §P basta per **misurare** e per **provare** un import, non per farlo: serve il significato dei codici
(`status`, `approved`, `type`…), che sta nel codice PHP di PATS o nella memoria dello staff, e un dump nuovo al momento
del passaggio. **Deciso** (§12 n.6): **un archivio in sola lettura** dei training di `trainingNEW` (2020–2026) e degli
esami di `exam`, mostrato sul percorso del trainee così com'è, senza rimapparlo sulle nuove schede; niente da `training`
(2014–2020, schema diverso, testi rovinati).

---

## 8. Che cosa chiede al nucleo

Ognuna è una PR a sé, **prima** del codice del modulo che la usa, con la sua nota (`CLAUDE.md` §0, regola 6).

| # | Estensione | Nota | Dove |
|---|---|---|---|
| 1 | **Ore di connessione**: leggere `hours` dal profilo IVAO al login e salvarle (forma del campo da misurare); il minimo dei dati IVAO, con uno scopo (piano §11.4, come l'email il 6 settembre) | sì, breve | §1.7, §2.2 |
| 2 | **Permessi del modulo a una posizione FIR, solo sul suo FIR**: un grant a CH e ACH, contato sulle righe `IHasFir` del loro FIR; nell'handler e nel guardiano. Oggi una posizione FIR non porta permessi, un grant a una posizione si scrive per dipartimento e `firStaffScope` vale per tutta la divisione | sì, con i test della spina dorsale | §3.2 |
| 3 | ~~Leggere il calendario di un giorno~~ **non serve**: il modulo legge il calendario in sola lettura, come FlightOps legge `hub_users` | — | §2.5 |
| 4 | **Il vocabolario dei rating** IVAO nel perimetro IVAO del nucleo — ATC e pilota: numero, **ordine**, sigla, nome tradotto, **quali hanno un training pratico**, **quale tipo di postazione** serve a ciascuno — con le domande che il modulo fa (§1.7); e il componente **`RatingBadge`** nell'elenco chiuso (previsto dal piano §8.3, rimandato ai moduli da `UI-GUIDELINES.md`): un badge **di testo**, niente immagini di IVAO. Se le postazioni di IVAO portano già il rating minimo, il legame postazione→rating si misura da lì (n.5) | sì, breve | §1.7 |
| 5 | **Le postazioni ATC** della divisione da IVAO (`ref_ivao_atc_positions` da `/v2/ATCPositions/all` e `/v2/subcenters/all`, campi da misurare), nella sincronizzazione notturna, con una directory come `IAirportDirectory` | breve (come gli aeroporti di M2) | §1.7 |
| 6 | **Il tipo `exam`** nel seme dei tipi del calendario (il piano §7 lo elenca già) | no | §5.1 |
| 7 | **Più permessi alternativi in scrittura** sulla stessa entità (`[AlsoWrittenWith]` ripetibile) e, per l'entità che lo dichiara, **anche alla creazione** | sì, con i test della spina dorsale | §3.4 |
| 8 | **Il banco e2e con rating e ore**: i personaggi di `/e2e/signin` oggi non hanno rating; il giro del training ne ha bisogno | nella nota di n.1 | §10 |
| 9 | ~~Un feed iCal personale, anticipato da M6~~ **tolta**: il feed non entra in M3, resta in M6 con la domanda aperta del piano §15.9 (Carmine, §12 n.14) | — | §12 n.14 |
| 10 | **«Persona cancellata»** nel nucleo: l'helper che mostra un VID negativo senza link, oggi nelle pagine dei tour; la nota della cancellazione lo fa passare nel nucleo quando un secondo modulo ne ha bisogno | no (già scritto nella nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona` §3) | §6.1 |

---

## 9. Tabelle e migrazioni

**Nucleo** (additive): colonne delle ore su `hub_users` (n.1), `ref_ivao_atc_positions` (n.5), le colonne del soggetto
FIR in `hub_user_grants` (n.2), il tipo `exam` (n.6).

**Modulo**: `trn_trainings`, `trn_slots`, `trn_sessions`, `trn_sheet_items`, `trn_evaluations`, `trn_exams`, `trn_bans`. La
migrazione `Initial` nasce nella fase dello scheletro con le tabelle di quella fase e non si tocca più; le altre
arrivano con la loro fase, sempre additive.

---

## 10. La rete di test

- **Unità**: il rating proposto e il trainer adatto **su un vocabolario di prova**, non sui numeri di IVAO (il
  vocabolario vero ha i suoi test nel nucleo); l'attesa per percorso (training, no-show, casella); il mock exam dalla
  storia; gli avvisi (altri training, voci del calendario, le tre politiche); lo stato «Eseguito» dal fuso della
  divisione; le soglie di ore; il ban con e senza scadenza.
- **Integrazione** (MariaDB vera, database condiviso: **VID `790001–790099`, slug `trn-test-`**): il ciclo con ogni stato
  e ogni uscita; nessuno approva, assegna, conduce o banna il proprio training (superadmin compreso); il trainer conduce
  il suo e non quello di un altro; il capo FIR assegna nel suo FIR e non in un altro; il trainee legge il suo training
  **senza** note riservate; una richiesta alla volta **per percorso**, ATC e pilota insieme sì; un TA crea un esame
  senza `Edit` (n.7) e un trainer no; un bannato non chiede; il promemoria parte una volta; il grant del trainer sparisce a
  training chiuso; **un trainer che è anche trainee non legge le note riservate del proprio training** dall'endpoint
  dello staff (§12 n.13, con la sua nota); **la cancellazione di una persona** (§6.1): i conteggi del registro uguali prima e dopo, nessun testo
  libero rimasto, i training aperti e gli esami del candidato spariti, il ban in vigore rimasto, «persona cancellata» nelle
  pagine.
- ⚠️ **Il TD nei test** (HANDOFF-M3, `CONTRIBUTING.md`): i test dei contatti affermano i destinatari esatti del TD, quindi
  **nessun TC o TAC seminato** nei test del modulo: i permessi si danno con grant a un VID. Da verificare se anche un
  T01–T99 entra nei destinatari.
- **Architettura**: il modulo non nomina IVAO (rating e postazioni dal nucleo) e **non scrive numeri di rating**;
  nessuna chiamata a IVAO se non dal client del nucleo.
- **Divisione XX**: nessuna stringa italiana, nessun ICAO, nessuna postazione nei semi e nei predefiniti.
- **Smoke e giro completo** (`pnpm e2e:full`): richiesta → accettazione → assegnazione → disponibilità → scelta → report,
  con i personaggi del banco estesi (n.8).

---

## 11. Ordine di lavoro proposto

Le fasi vere si scrivono in `08-piano-implementazione-m3.md` dopo l'approvazione; qui la forma. Una fase, un branch
`m3/<fase>-<slug>`, una PR.

| Fase | Contenuto |
|---|---|
| A0 | Le note di decisione delle scelte di §12 e delle estensioni; `08-piano-implementazione-m3.md` |
| A1 | Nucleo: ore di connessione, vocabolario dei rating e `RatingBadge`, banco e2e con rating e ore (n.1, n.4, n.8) |
| A2 | Nucleo: postazioni ATC da IVAO, tipo `exam` (n.5, n.6) |
| A3 | Nucleo: più permessi alternativi in scrittura, anche alla creazione (n.7) |
| A4 | Modulo: scheletro, `Initial`, catalogo, `positionGrants`, impostazioni, menu, segmento riservato |
| A5 | Voci della scheda |
| A6 | La richiesta: form, controlli per percorso, domanda sul teorico, annullamento, `/training/mine`, mail |
| A7 | Accettare, rifiutare, assegnare (TC e TAC); il grant del trainer e il job che lo toglie |
| A8 | Le date: disponibilità, avvisi, scelta a riquadri, override, calendario, promemoria, chiusura per tempo |
| A9 | Dopo la sessione: rischedula, no-show, scheda con N/A, report, mock exam |
| A10 | Blocchi Data, pagina pubblica della sessione, percorso del trainee, esami nel calendario, ban |
| A11 | Nucleo: i capi FIR (n.2); poi nel modulo l'assegnazione e le liste per FIR |
| A12 | Conservazione; `TrainingPersonalData : IPersonalDataEraser` e «persona cancellata» nel nucleo (n.10); archivio di PATS se otteniamo il significato dei codici (n.6); giro completo |

I capi FIR stanno in fondo di proposito: tutto il resto funziona senza, e l'estensione più delicata non blocca il modulo
(§12 n.3). **Il feed del calendario dei trainer non è in M3** (§12 n.14): a M3 chiusa **PATS resta acceso solo per quel
feed**, finché l'iCal del nucleo non arriva in M6.

---

## 12. Domande per Carmine — decise il 25 settembre 2026

Carmine ha risposto sulla PR #121: alle domande n.1–14 nel [commento delle risposte][r1], alla n.7 riformulata e alla
n.15 nel [secondo giro][r2], e ha precisato la n.5 in [un terzo commento][r3]. Qui sotto ogni domanda con la
raccomandazione e la decisione; ognuna entra nella nota della fase A0.

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832705237
[r2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5832839987
[r3]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/121#issuecomment-5833005647

1. **Il trainer sul suo training** (§3.3). Raccomandato: il grant con scope per training. **Deciso** ([r1]): **come
   raccomandato** — un grant `Training.Conduct` con lo scope del training, scritto all'assegnazione con `ModuleGrants`,
   e il job notturno che toglie i grant dei training chiusi. Il login in più a ogni assegnazione è accettato.
2. **Più permessi alternativi in scrittura** (§3.4, n.7). **Deciso** ([r1]): **sì** — `[AlsoWrittenWith]` si ripete, e
   un'entità che lo dichiara lo usa anche alla creazione. È un cambio del nucleo: una PR a sé, con una nota nuova e i test
   della spina dorsale, prima del codice del modulo che lo usa (fase A3).
3. **I capi FIR** (n.2). **Deciso** ([r1]): **sì, come fase A11**, dopo che il modulo funziona con TC e TAC.
4. **Il VID nel calendario pubblico** (R.6-bis). **Deciso** ([r1]): **niente VID ai visitatori**; VID e nomi solo a chi ha
   fatto il login, sulla pagina della sessione (piano §9.7, il minimo necessario).
5. **Le postazioni** (n.5). **Deciso** ([r3]): **da IVAO**, con il legame postazione→rating nel **vocabolario del
   nucleo** (n.4) e nelle impostazioni **solo `hiddenPositions`**; nessun `facilityRatings`. (La prima risposta, in [r1],
   nominava `facilityRatings` nelle impostazioni perché era scritta sul testo di prima della correzione n.3; Carmine l'ha
   corretta in [r3].)
6. **Lo storico di PATS** (§7). **Deciso** ([r1]): **un archivio in sola lettura** di `trainingNEW` ed `exam`, mostrato
   com'è sul percorso del trainee, **solo se** otteniamo il significato dei codici; niente da `training` (2014–2020).
7. **Che cosa tiene il registro dei training alla cancellazione** (§6.1). **Deciso** ([r2]): **come raccomandato** — i
   training chiusi restano con lo pseudonimo e senza nessun testo libero; quelli aperti e gli esami del candidato si
   cancellano; un ban in vigore resta con VID e motivo. È la regola della nota
   `2026-09-25-la-cancellazione-dei-dati-di-una-persona`. (La risposta di [r1] era sulla domanda prima della
   riformulazione, e dice lo stesso: il registro resta, il meccanismo è quello di T20b, il modulo non ne scrive uno suo.)
8. **Group training, GCA, flight briefing** (§0.2). **Deciso** ([r1]): **fuori da M3**, da riprendere se lo staff TD li
   chiede.
9. **Tempo massimo per scegliere la data** (§2.5). **Deciso** ([r1]): **chiusura automatica** (`Closed`), solo se
   l'impostazione c'è; di default non c'è.
10. **Gli esami nel calendario** (§2.8). **Deciso** ([r1]): li inserisce **chi ha l'esame assegnato**, quindi
    `Training.ManageExams` a tutto lo staff del training, e la creazione passa il guardiano con la n.7. **Corretta il 26 settembre
    2026** ([commento sulla #131](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/131#issuecomment-5844102750), nota
    `2026-09-26-gli-esaminatori`): gli esaminatori sono HQ, TC, TAC e i TA, mai i trainer; `Training.ManageExams` va a TC, TAC e TA.
11. **Le voci della scheda** (§1.4). **Deciso** ([r1]): **tradotte**, `Localized` con tutte le lingue della divisione.
12. **Il sito dell'esame teorico** (§2.2). **Deciso** ([r1]): **un'impostazione** (`theoryExamUrl`), così il modulo non
    nomina IVAO.
13. **Un trainer che è anche trainee** (§3.1). **Deciso** ([r1]): l'endpoint dello staff **toglie le note riservate
    quando chi legge è il trainee della riga**. Perché è una regola su chi legge che cosa, scritta a mano, ha **una sua
    nota di decisione** (fase A0) e **un test d'integrazione** (§10).
14. **Il feed del calendario dei trainer** (d4). **Deciso** ([r1]): **non in M3**. Il feed iCal resta in M6, con la
    domanda aperta del piano §15.9; l'estensione n.9 e la sua fase sono uscite dal design. Fino a M6 i trainer usano il
    feed di PATS: **PATS resta acceso solo per quello**, quando tutto il resto è passato all'hub.
15. **Il teorico** (§0.6, §2.2). **Deciso** ([r2]): **come raccomandato** — lo dichiara il trainee, dietro
    `ITheoryExamSource`, e chi approva lo controlla su IVAO. È lo scostamento dal piano §9.2 e §14 che il piano
    registrerà dopo il merge (§14).

---

## 13. Confermato con `dalberone`

Tutti i fatti e le preferenze del TD chiesti durante la stesura sono confermati: R.1–R.7, l'ultimo giro il 25 settembre
(R.7). Nulla resta aperto da quel lato; le scelte sono in §12.

---

## 14. Da portare nel piano

Lo scrive il revisore dopo il merge (`CLAUDE.md` §0); le note della fase A0 lo ripetono per le loro decisioni.

- **§9.2, riga Training**, e **§14, rischio dello scope `training`**: il teorico lo **dichiara il trainee**, dietro
  `ITheoryExamSource`, e chi approva lo controlla su IVAO; non gli esiti inseriti dallo staff a mano o da CSV (§12 n.15).
- **§9.2, riga Training**: group training fuori da M3 (§12 n.8); lo storico di PATS come archivio in sola lettura di
  `trainingNEW` ed `exam`, solo se si ottiene il significato dei codici (§12 n.6). **§15.7**: il dump di PATS c'è (12
  settembre 2026), e i codici restano da capire.
- **§13, riga M3** («Spegne `training.ivao.it`»): PATS resta acceso **solo per il feed del calendario dei trainer** fino
  all'iCal del nucleo in M6 (§12 n.14); **§15.9** resta aperta. Le fasi sono A0–A12 di `08-piano-implementazione-m3.md`.
- **§8.2, sitemap**: le pagine del membro sotto `/training`, e il blocco `training.myTraining` in `/me` (§0.4).
- **§9.7, «Privacy dei membri»**: nel calendario pubblico nessun VID ai visitatori (§12 n.4).
- **§16 e `CLAUDE.md` §2**: le estensioni del nucleo n.2 (i capi FIR) e n.7 (`[AlsoWrittenWith]` ripetibile, anche alla
  creazione), ciascuna con la sua nota nella sua fase (A11, A3).
