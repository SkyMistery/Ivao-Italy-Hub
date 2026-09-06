# L'indirizzo di un destinatario

**Data:** 6 settembre 2026 — trovata aprendo G7
**Stato:** **decisa il 6 settembre 2026 da Carmine: A, più le caselle di dipartimento**
(§«La decisione»)
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: serve un dato che nessun documento prevede di
avere. Ci si ferma, si scrive, si decide, poi si codifica.

## Cosa serve

G7 costruisce il servizio notifiche, e la sua prova di accettazione è «un messaggio dal form arriva
in Mailpit nella lingua del destinatario». Per mandare una mail serve un indirizzo.

## Che cosa esiste oggi, verificato

- **L'hub non conserva nessun indirizzo email.** `hub_users` ha VID, nome, nickname, divisione,
  paese, rating, lingua, flag di staff: nessuna colonna di contatto.
- **Il payload di IVAO lo porta, e il codice lo scarta apposta.** `IvaoUserProfileReader` lo dice in
  un commento: «The payload also carries an email address, which is deliberately not read: the hub
  keeps the minimum IVAO data it needs (plan section 6.4)». Il campo `email` è stato misurato nel
  payload vero il 3 settembre 2026.
- **Lo scope `email` è già chiesto al login**, in `config/ivao-oauth.json` e nell'esempio:
  `["openid", "profile", "email", "discord"]`. Il dato arriva già a ogni accesso e viene buttato.
- **Il piano se lo aspetta in due punti.** §11.4 (GDPR) scrive «dati IVAO minimi (**niente email se
  non serve al modulo**)» — cioè: si conserva quando serve; e §9.7 dice che ogni cambio
  dell'insieme dei superadmin «genera una **email a tutti i superadmin**», che `SuperadminService`
  rimanda a M1 con un commento: *«The email to every super administrator arrives in M1, with the
  notification service.»*
- **Il design M1 §5.2 vuole il destinatario come persona**: l'intento porta «la lingua **del
  destinatario**», e le preferenze sono per VID (`hub_notification_preferences`). Una coda che
  scrive a una casella sola non avrebbe né una lingua per destinatario né una preferenza per VID.

Nessun meccanismo esistente copre il caso: non è un'estensione di `MapCrud`, del generatore di form
o del registry. È un dato che oggi non c'è.

## Il bivio

| | A — l'indirizzo del membro (raccomandata) | B — una casella per dipartimento | C — nessuna mail in M1 |
|---|---|---|---|
| Dove sta | colonna `Email` su `hub_users`, riempita a ogni login da `IvaoUserProfileReader` | una riga per dipartimento in `hub_division_settings`, scritta dallo staff | da nessuna parte |
| Destinatario dell'intento | VID | un indirizzo fisso | un indirizzo di prova |
| Lingua del destinatario | quella del membro, che l'hub già conosce (`hub_users.Locale`) | non esiste: la casella non ha una lingua | finta |
| `hub_notification_preferences` | ha senso: è per persona | non ha senso: nessuno a cui applicarla | non ha senso |
| Dato personale in più | sì, uno: l'indirizzo | nessuno | nessuno |
| Costo | una colonna, una riga nel reader, un test che nessun DTO la espone | una colonna, una schermata per scriverla, e ogni dipartimento deve avere una casella vera | zero, ma la fase non dimostra niente |
| Contro | il commento del reader va riscritto: era una scelta deliberata | la coda di M2 e M3 (iscrizione a un evento, esito di un esame) scrive **alla persona**, non a un ufficio: la forma andrebbe rifatta alla prima notifica vera | rimanda il problema alla prima fase che ha bisogno di scrivere a qualcuno |

**Raccomandazione: A.** L'unica forma di `NotificationIntent` che M2 e M3 useranno senza toccarla è
quella che ha per destinatario una **persona**: un'iscrizione confermata, un esame assegnato e un
tour completato si scrivono al membro, non a una casella di reparto. E il dato non è nuovo — arriva
già a ogni login, con uno scope che il membro ha già concesso; oggi lo si butta.

Con A, tre righe di guardia che vanno costruite nella stessa fase e non dopo:

1. **Nessun DTO porta l'indirizzo.** Vale già per la staff directory di G9
   (`StaffDirectoryExposesNoContactData`); qui diventa un test che vale per tutto: la colonna la
   legge solo il servizio notifiche.
2. **Un membro senza indirizzo non è un errore.** Chi ha fatto login prima di questa release non ce
   l'ha finché non rientra: la coda salta il destinatario e lo scrive in `hub_notifications`, non
   fallisce.
3. **Cancellare l'indirizzo resta possibile**: è una colonna nullable, e svuotarla spegne le mail di
   quel membro senza toccare altro.

## Che cosa si tocca, con A

- `HubUser.Email` (nullable), migrazione additiva.
- `IvaoUserProfileReader`: legge `email`; il commento cambia da «deliberately not read» a «letto per
  il servizio notifiche, e da nessun'altra parte».
- `IvaoUserProfile` e `UserSyncService`: un campo in più che segue la stessa strada di `Locale`.
- Il servizio notifiche: risolve VID → indirizzo + lingua in un punto solo.
- Un test: nessun DTO dell'API contiene la parola.

## La decisione (6 settembre 2026)

**A, e anche le caselle di dipartimento.** Le due cose non sono alternative: una divisione ha
indirizzi che raggiungono un intero dipartimento, e servono; ma servono anche quelli personali,
perché a un membro si scrive al membro. Quindi un destinatario è **una delle due cose**, e l'intento
lo dice:

- `NotificationRecipient.Member(vid)` — indirizzo e lingua risolti al momento dell'invio da
  `hub_users`. È la forma che M2 e M3 useranno (iscrizione confermata, esito di un esame).
- `NotificationRecipient.Mailbox(address)` — un indirizzo fisso, che non appartiene a nessuno; la
  lingua è quella di default della divisione, perché una casella non ne ha una.

**Dove stanno le caselle**: in `config/division.json`, chiave `departmentMailboxes`, mappa da codice
di dipartimento a indirizzo, **facoltativa** — un dipartimento senza casella riceve solo sulle
persone, e una divisione che non ne ha nessuna non scrive la chiave. È configurazione del
comportamento della divisione (piano §4.1), non contenuto: non è una tabella, non è una schermata, e
chi forka mette le proprie. Validata all'avvio come il resto di `DivisionOptions`: le chiavi devono
essere dipartimenti che esistono, i valori devono avere la forma di un indirizzo.

**Un messaggio di contatto va quindi in due posti**: alla casella del dipartimento destinatario, se
c'è, e ai membri dello staff di quel dipartimento che hanno la preferenza accesa. Le tre guardie di
sopra restano tutte e tre.
