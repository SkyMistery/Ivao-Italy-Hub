# Il ban chiude ciò che è aperto

**Data:** 1 novembre 2026 — fase di test (nota `2026-10-18-la-fase-di-test`), issue #312, branch `fix/training-ban-closes-open`,
PR #433
**Stato:** **decisa** da Carmine per il §2, in chat al master il 19 ottobre 2026, pubblicata sulla issue su sua istruzione
([risposta][a312]); **decisa** da Carmine anche per il §4 (la sessione già cominciata): la (A), in chat al master il 1° novembre 2026,
pubblicata sulla PR su sua istruzione ([risposta][a433]).
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione nuova del modulo, decisa dal maintainer. I meccanismi sono quelli che ci
sono (la CRUD generica con il suo `BeforeSave`, `AlsoWrittenWith`, le notifiche del nucleo, la proiezione nel calendario): niente del
nucleo cambia.

[a312]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/312#issuecomment-6269152656
[q433]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/433#issuecomment-6551675321
[a433]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/433#issuecomment-6555197149

## 1. Che cosa serve

La revisione del master nella fase di test (issue #312) ha trovato che un ban (`TraineeBan`, design M3 §1.5-bis, §2.9) ferma solo le
richieste **nuove**: i training già aperti vanno avanti, e un trainee bannato può ancora scegliere la data di un training già affidato a
un trainer (`TrainingDates.ChooseAsync` non guarda i ban). Carmine vuole il contrario. Con due punti della stessa revisione:

1. `TraineeBan` non dichiara `[AlsoWrittenWith(TrainingPermissions.Ban)]`: il controllo delle scritture ripiega su `Training.Edit` per
   dare un ban e per «Togli ban», e chi ha `Training.Ban` senza `Training.Edit` è rifiutato. Oggi nessuno è in quel caso
   (`division.json` dà il ban solo a chi ha anche Edit), ma il catalogo e il controllo non dicono la stessa cosa.
2. Il comportamento nuovo cambia il design, che dice «quelli già aperti vanno avanti».

## 2. Che cosa ha deciso Carmine

**Quando un membro è bannato, tutto ciò che ha aperto si chiude da solo** («se un membro è bannato tutte le sue richieste decadono e un
eventuale richiesta training si blocca e viene chiusa in automatico»):

- le richieste ancora **in attesa** si chiudono subito;
- un training **già affidato** a un trainer, o **con la data fissata**, si chiude anche lui: **il trainer riceve una notifica e la data
  si libera** («il trainer riceve una notifica e la data si libera»);
- quando il ban finisce o si toglie **non si riapre niente**: il membro chiede di nuovo (la proposta del master, accettata: «è giusto
  come dici tu»).

E nella stessa PR `TraineeBan` prende il suo `[AlsoWrittenWith(TrainingPermissions.Ban)]`, così `Training.Ban` da solo basta per dare
un ban e per toglierlo, come dice il catalogo.

## 3. Come lo fa il codice

Sono letture della risposta, non scelte nuove; le scrivo perché chi rivede le controlli.

- **Che cosa chiude**: ogni training del membro ancora aperto — `Requested`, `Accepted`, `Assigned`, `Scheduled` con la sessione ancora
  da venire (`TrainingBans.Closable`). Lo stato è `Closed`, come le altre chiusure: non `Rejected`, perché nessuno ha deciso sulla
  richiesta, né `Cancelled`, perché il trainee non l'ha ritirata.
- **Chi chiude e perché**: chi dà il ban (`closed_by`), **senza motivo**. Il motivo del ban resta del ban: i capi di un FIR leggono i
  training del loro FIR ma nessun ban (A11b, nota `2026-09-27-i-capi-fir-sul-loro-fir`), e copiarlo nel training glielo mostrerebbe.
- **Come si riconosce**: una colonna nuova, `trn_trainings.closed_by_ban` (migrazione additiva `AddClosedByBan`, `false` per le righe
  che ci sono). Senza, la pagina del trainee — che non dice chi ha chiuso — leggerebbe la chiusura come quella dell'hub per una data mai
  scelta. Le due pagine e lo storico (`ClosedByBan`) dicono «chiuso per il ban», e lo staff vede chi l'ha dato.
- **La data si libera**: le date proposte vanno con il training, e la sessione datata esce dal calendario (un training chiuso non si
  proietta), quindi il giorno del trainer è libero anche per i conflitti delle date (§2.5). La data resta scritta nel registro, come in
  ogni chiusura (§6).
- **Le mail**, nella lingua di chi le riceve: il trainee riceve la mail del ban, la cui frase «i training che hai già aperti vanno avanti
  come sono» se ne va; al suo posto, solo quando il ban ha chiuso qualcosa, «i training che avevi aperti e non ancora cominciati sono
  chiusi, e non si riaprono quando il ban finisce» (un frammento, `bannedClosed`, come la data in quella del trainer: la revisione del
  master); un membro senza niente di aperto, o con la sola sessione già cominciata del §4, non legge di training chiusi. Il trainer di ogni training chiuso che ne aveva uno
  (`Assigned`, `Scheduled`) riceve la notifica nuova `training.closedByBan`, con la data della sessione se c'era, e senza il motivo del
  ban. Una richiesta senza trainer non avvisa nessun altro.
- **Nella stessa transazione del ban**: il `BeforeSave` della CRUD dei ban chiude i training, e il salvataggio del ban li scrive insieme
  a lui. Scriverli chiede un permesso sui training a chi dà il ban: `Training` prende anche lui `[AlsoWrittenWith(TrainingPermissions.Ban)]`
  — senza, chi ha solo `Training.Ban`, che ora basta, sarebbe rifiutato proprio su quei training. Nessun endpoint scrive un training con
  `Training.Ban` se non il ban: ogni passo dello staff chiede prima il suo permesso sulla riga.
- **Le due scritture che possono incontrare un ban**:
  - un passo su uno dei training (il trainee che sceglie la data, lo staff che assegna): la versione della riga, e chi salva per secondo
    ha il 409 — se è il ban, non si scrive niente di lui;
  - una richiesta mandata mentre si dà il ban, che è una riga nuova e nessuna versione la protegge: dopo il salvataggio del ban si
    rileggono i training ancora aperti e si chiudono; dopo il salvataggio di una richiesta si rileggono i ban, e se uno vale la richiesta
    si chiude e il trainee legge «sei bannato». Delle due letture una vede sempre la riga dell'altra già scritta: se la richiesta è
    scritta prima della rilettura del ban, la vede il ban; se no, la sua rilettura dei ban viene dopo il salvataggio del ban. Nessun lock,
    quindi nessun deadlock fra le due. Se la rilettura del ban incontra un conflitto di versione per tre volte di fila, lascia stare e
    lo scrive nel log come warning (la revisione del master): una richiesta fra quei training la chiude comunque la sua rilettura dei ban.
  - una richiesta che la sua rilettura trova sotto un ban resta come riga chiusa per il ban nel percorso del membro, che ha letto «sei
    bannato»: è il prezzo di non avere un lock, e lo storico dice che cosa è successo.
- **Il rinvio sotto un ban** (§4): se il ban vale già quando il trainer rinvia, il rinvio e la chiusura sono **un solo salvataggio**; la
  rilettura dei ban dopo il salvataggio resta per un ban salvato nel frattempo, e solo allora sono due. L'endpoint risponde con la
  pagina del training, che dice «chiuso per il ban»; lo storico dice prima il rinvio del trainer, poi la chiusura.
- **`Training.Ban` e il controllo delle scritture**: con l'alternativa su `Training` il controllo lascia passare chi ha `Training.Ban`
  su ogni scrittura di un training, ma nessun passo dello staff la usa — ognuno chiede il suo permesso sulla riga prima —; un test lo
  fissa (`TrainingBanAloneTakesNoStepOnATraining`).
- **La scelta della data** non ha un controllo del ban suo: il training è chiuso e le sue date proposte sono andate con lui.

## 4. La sessione già cominciata — **decisa: (A)**

**Una sessione già cominciata** — la data è passata, l'esito non è ancora registrato — quando arriva il ban. Qui si incontrano due
risposte del maintainer: questa («con la data fissata si chiude anche lui») e quella sulla #149 («una sessione cominciata si registra, e
non si data più a mano né si chiude»). La domanda è posta a Carmine sulla PR ([commento][q433], 1 novembre 2026), in inglese, con la (A)
raccomandata. **Carmine ha scelto la (A)** ([risposta][a433]): «It is not closed; the trainer records its outcome, and if they
postpone it while the ban holds, the training is closed by the ban».

- **(A), scelta, ed è quello che fa il codice**: non si chiude. Il trainer ne registra l'esito — report, no-show o rinvio —; se la
  rinvia mentre il ban vale, il training non torna alle date (un membro bannato non ne ha) ma si chiude per il ban, da chi l'ha dato.
- **(B)**: si chiude anche lei, come le altre. La sessione, forse tenuta, non si registra più: né il report né il no-show.

La (A) tiene la risposta sulla #149 e non perde il report di una sessione che c'è stata; con la (B) il codice cambia in una riga
(`TrainingBans.Closable` prende anche le sessioni cominciate) e il rinvio non chiede più niente al ban.

## 5. Le alternative scartate

- **Il motivo del ban nel motivo della chiusura**: lo leggerebbero i capi dei FIR, che non leggono i ban (§3).
- **Uno stato nuovo** (`Banned`) invece della colonna: ogni lista, ogni regola e ogni pagina che guarda gli stati lo dovrebbe imparare,
  anche dove un training chiuso è chiuso e basta. La colonna dice solo il perché.
- **Un lock che il ban e la richiesta prendono tutti e due** (`SELECT … FOR UPDATE`): nel modulo non c'è una riga del membro che
  entrambi possano bloccare, e un lock sui ban preso dalla richiesta contro quello sui training preso dal ban apre un deadlock. Le due
  riletture dopo i salvataggi bastano (§3).
- **Chiudere dopo il salvataggio del ban, in una seconda scrittura sola**: non sarebbe la stessa transazione, e per un attimo un membro
  bannato avrebbe i suoi training aperti. La seconda lettura resta, ma solo per la richiesta in volo.

## Da portare nel piano

- **§9, il catalogo, la voce Training**: un ban chiude i training aperti del membro, chi lo dà ne è l'autore, il trainer riceve
  `closedByBan` e la data si libera; finito il ban non si riapre niente. `Training.Ban` da solo dà e toglie un ban.
- **Il design M3** (`07-design-m3.md`) è aggiornato in questa PR: §1.5-bis, §2.9, §5.2.
- **La risposta al §4**: una sessione già cominciata quando arriva il ban non si chiude; il trainer ne registra l'esito, e se la rinvia
  mentre il ban vale il training si chiude per il ban (la (A), [risposta][a433]).
