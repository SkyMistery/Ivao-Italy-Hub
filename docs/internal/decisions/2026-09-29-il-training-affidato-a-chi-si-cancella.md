# Il training aperto affidato a chi si cancella (A12b)

**Data:** 29 settembre 2026 — fase A12b di M3, PR del modulo #189, in coda dopo #187
**Stato:** **Proposta** — la domanda di §4 è per Carmine, in [un commento sulla #189][q1]
**Regola applicata:** `CLAUDE.md` §5, caso **(c)** per la parte che chiede una scelta: che cosa fa il modulo di una riga **aperta di
un altro membro**, affidata a chi si cancella, che la regola del modulo (nota `2026-09-25-la-cancellazione-dei-dati-di-un-trainee`)
non dice. Il resto di A12b — l'eraser sui dati del trainee, «persona cancellata» nelle pagine, la conservazione — è (b) e non
aspetta.

[q1]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/189#issuecomment-5898971168

## 1. Che cosa serve decidere

Un trainer chiede che i suoi dati siano cancellati mentre un training di un altro membro è **affidato a lui ed è ancora aperto**
(`Assigned`, magari con le sue date proposte, o `Scheduled`, con la data). Lo stesso per un esaminatore con un esame ancora da tenere.

La regola del modulo parla dei training **del trainee** — chiusi: il registro; aperti: via — e di **ciò che la persona ha fatto come
trainer**, che resta con lo pseudonimo (risposta 4 della nota di T20b). Non di questo. Che cosa succede oggi, senza scriverne niente
(A7b, «Trovato» n.4; A12a, «Trovato» n.4):

- il nucleo scrive lo pseudonimo in `trainer_vid` come in ogni colonna di persona: il training resta **affidato a nessuno che esista**;
- lo conduce solo chi tiene `Training.Edit` (TC, TAC): `Training.Conduct` raggiunge le righe affidate a chi scrive, e nessuno è lo
  pseudonimo;
- nella lista dello staff sta «in corso» (o «da chiudere»), non «da assegnare»: **nessuna vista lo segnala**;
- se ha date proposte, il trainee può ancora sceglierne una; se ha la data, il promemoria parte al trainee (quello al trainer non ha a
  chi andare) e **nomina il trainer con il numero** (`TrainingPeople.Label`), come la mail della data fissata;
- un esame affidato a un esaminatore cancellato resta nel calendario, e lo cambia solo chi tiene `Training.Edit`.

## 2. Che cosa il modulo non può fare da solo

L'eraser gira nella **modalità cancellazione** dell'interceptor (nota di T20b §3): una riga auditata cambiata in qualcosa che non sia una
colonna di persona è registrata come `erased`, senza dati, e **le sue copie nell'audit si svuotano** (`CollectErasure`,
`AuditRedaction.EmptyAsync`). Rimettere il training «Accettato», o chiuderlo, dall'eraser cancellerebbe **la storia d'audit del
training di un altro membro**. Farlo senza quell'effetto vuole il nucleo: un'altra fase, con la sua nota.

## 3. Le strade

| | Strada | |
|---|---|---|
| **(a)** | **Resta affidato alla persona cancellata, e torna fra quelli da assegnare** — raccomandata | L'eraser non lo tocca. L'anteprima e il risultato della cancellazione lo contano (una riga del modulo: «training aperti affidati a lui: restano a una persona cancellata, da riassegnare»), così il superadmin lo sa prima di confermare. La vista **«da assegnare»** della lista dello staff (`StaffQueue.ToAssign`), e con lei il blocco `training.approvalQueue`, prende anche un training aperto con il trainer cancellato (`trainer_vid < 0`), che esce da «in corso» e «da chiudere»: lo staff lo trova dove trova i training senza trainer e lo dà a un altro con «Assegna», come ogni cambio di trainer (A7: un training datato tiene la data, le date proposte vanno). Le mail che nominano il trainer — la data fissata, il promemoria — dicono «Persona cancellata» (`people.deleted` del nucleo, dal catalogo del server, come prevede la nota di A12a §3 punto 2), non il numero. Un esame affidato a un esaminatore cancellato resta com'è, con «Persona cancellata» nella lista: l'esame è della rete (R.6) e lo cambia chi tiene `Training.Edit`; nessuna vista nuova. Tutto nel modulo, nessun file del nucleo. |
| (b) | L'eraser lo rimette «Accettato», senza trainer né data (o lo chiude) | Il più pulito per il trainee, ma dall'eraser svuoterebbe la storia d'audit del training di un altro membro (§2): vuole il nucleo. |
| (c) | Resta affidato, e basta | Nessuna riga oltre alle pagine; ma nessuna vista lo segnala, il trainee può scegliere una data che nessuno condurrà, e il promemoria gli annuncia una sessione con «un trainer» che non c'è, finché qualcuno non se ne accorge. |

**Raccomandazione: (a).** Il training resta come la regola delle colonne lo lascia (la risposta 4: ciò che la persona ha fatto come
trainer resta con lo pseudonimo), ma non si perde: torna dove lo staff guarda per dare un trainer, e le mail non scrivono un numero.

## 4. La domanda

| # | Domanda | Raccomandazione | Le altre |
|---|---|---|---|
| 1 | Un training aperto di un altro membro, affidato a un trainer che si cancella (e un esame affidato a un esaminatore che si cancella): che cosa ne fa il modulo? | **(a)**: resta affidato alla persona cancellata; l'anteprima lo conta; torna nella vista «da assegnare» (e nel blocco) finché qualcuno non lo riassegna; le mail dicono «Persona cancellata». L'esame resta com'è | (b) rimesso «Accettato» o chiuso dall'eraser: vuole il nucleo; (c) resta affidato, e basta |

**Che cosa aspetta la risposta**: soltanto questa parte — la riga dell'anteprima, la vista, le parole delle mail — con i suoi test.
Fino ad allora il training resta come lo lascia la regola delle colonne, cioè come (c).

## 5. Che cosa si tocca, con (a)

`src/IvaoHub.Modules.Training/TrainingPersonalData.cs` (la riga), `Staff/StaffQueue.cs` (le tre viste), `TrainingMail.cs` (la
parola per uno pseudonimo in una mail), le parole del modulo (`erasure.*`), i test d'integrazione (la vista, la riga, la mail del
promemoria). Nessun file del nucleo.

## Da portare nel piano

- Niente nel piano: §9.7 e §16 punto 16 dicono già il percorso. La regola del modulo sta nel design `07-design-m3.md` §6.1, che la PR di
  A12b completa con la risposta.
