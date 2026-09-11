# I tipi di evento sono di divisione: dove metterli

**Data:** 8 settembre 2026 — G13, richiesta 11 della demo
**Stato:** **decisa da Carmine l'8 settembre 2026 — opzione 1**, e costruita lo stesso giorno
(`8b6458c`). Questa nota resta com'era scritta, con in fondo che cosa è costata davvero e la
**correzione di un errore che conteneva**.
**Perché esiste:** regola (c) di piano §16.E — serve un meccanismo che non c'è (un vocabolario **di
divisione**), e il piano di implementazione di M1 dice per questa richiesta «da proporre prima di
scriverlo».

## Che cosa ha chiesto Carmine

> «L'elenco è deciso da HQ, da WD o dalla sezione admin e sono quelli per tutti.»

Oggi il `kind` di una voce di calendario è **testo libero**: `event`, `training`, `tour`, `meeting`,
`deadline`, più quello che un modulo proietta con le sue righe. Chi scrive una voce lo digita, e due
dipartimenti che scrivono `Training` e `training` hanno due tipi.

⚠️ **Non sono le categorie.** `cms_categories` è **per dipartimento** (§3.4 del design M1): serve
esattamente per il caso opposto, cioè scaffali che ogni dipartimento decide da sé.

## Il punto che va deciso, ed è uno solo

Ogni riga dell'hub ha un `owner_department`, e su quello poggiano tre cose senza eccezioni: chi può
modificarla, dove compare nel back-office, come si filtra sul sito. **Un vocabolario di divisione
non ha un dipartimento** — e questa è la prima riga del prodotto che non ne ha.

Tre modi, in ordine di quanto costano:

1. **Una tabella con `MapCrud` e `SharedForReading`, senza `IOwnedByDepartment`.** La tabella è
   `cms_calendar_kinds` (chiave, etichetta tradotta, colore, ordine). Il motore CRUD la serve come
   qualunque altra risorsa; la legge tutto lo staff (`SharedForReading` esiste già ed è nato per
   questo), la scrive chi ha un permesso nuovo — `Calendar.ManageKinds` — che, **non essendo la riga
   di nessun dipartimento**, si concede globalmente dalla matrice dei ruoli: Director, Assistant
   Director, WM, AWM, superadmin.
   ⚠️ Il costo vero è qui: l'unico authorization handler dell'hub confronta le posizioni con
   `owner_department`, quindi una risorsa senza dipartimento **non passa da lui**. Va detto in un
   posto solo — «questa risorsa si autorizza sul permesso e basta» — ed è un'opzione di `MapCrud`,
   non un secondo handler.
2. **Una riga di `hub_division_settings` con dentro un JSON.** Zero tabelle, zero permessi nuovi
   (basta `Admin.Access`), ma niente schermata: si modifica dove si modificano le impostazioni, che
   **oggi non hanno una schermata**. Costa poco adesso e lascia un buco a forma di CRUD.
3. **Riusare `cms_categories` con `owner_department` = il dipartimento del sito.** Non costa niente
   e mente: un vocabolario di tutti che appartiene a WD è una riga che il giorno dopo qualcuno
   sposta o cancella «perché è di WD».

## La raccomandazione

**La 1.** È il vocabolario che Carmine ha descritto, si scrive con il motore che c'è, e la cosa che
apre — «una risorsa che non appartiene a un dipartimento» — è una porta che M2 aprirà comunque:
i tipi di slot degli eventi e i livelli di training sono lo stesso problema. Meglio aprirla una
volta, con un nome, che tre volte di nascosto.

Quello che porta con sé, se la decisione è questa:

- una tabella e una migrazione additiva;
- un permesso (`Calendar.ManageKinds`) e la sua riga nella matrice dei ruoli;
- un'opzione di `MapCrud` per una risorsa senza dipartimento — ⚠️ **il pezzo da guardare con
  attenzione**, perché tocca la spina dorsale;
- una schermata sotto `/staff/admin`, generata come tutte le altre;
- il `kind` del form del calendario che diventa una select sul vocabolario (precedente esatto: le
  categorie delle news, design M1 §3.4);
- il **colore** che si sposta sulla riga, e `calendarKindColour` che sparisce insieme alla mezza
  pagina di tinte derivate dalla parola che G13 ha scritto per intanto.

## Che cosa succede se si decide di non farla adesso

Niente si rompe. Il calendario resta a testo libero, la chip resta colorata per parola, e la
richiesta 11 resta aperta con questa nota accanto. È l'unica delle dodici che tocca la spina
dorsale, ed è l'unica che il piano di implementazione dice di proporre prima di scrivere.

---

## Che cosa è costata, e l'errore che questa nota conteneva

⚠️ **«Sarebbe la prima riga senza `owner_department`» era sbagliato**, ed era il punto su cui la
nota chiedeva a Carmine di decidere. I **grant** ci sono arrivati in M0: `GrantEndpoints` dice a
chiare lettere di essere «la prima risorsa dell'hub senza nessun dipartimento», e per servirla il
motore CRUD ha già la modalità globale — `ReadPolicy` e `WritePolicy` al posto di un'area di
permessi. Quindi **la spina dorsale non è stata toccata**: non c'è nessuna opzione nuova di
`MapCrud`, e il pezzo che la nota segnava come «da guardare con attenzione» non esisteva.

Il resto del conto è quello previsto: una tabella (`cms_calendar_kinds`), una migrazione additiva,
un permesso globale (`Calendar.ManageKinds`) che i ruoli che raggiungono ogni dipartimento hanno per
il fatto stesso di essere globale — nessuna riga nella matrice — una schermata generata sotto
`/staff/admin`, e il `kind` del form del calendario che diventa una select.

Due cose in più, non previste e giuste:

- **Il `kind` di una voce non è più testo libero.** Il validatore chiede al vocabolario, ed è
  l'unico validatore dell'hub che fa una domanda al database. Una voce proiettata da un modulo non
  passa da quel DTO e non viene toccata.
- **Il vocabolario viaggia in `/api/me`.** Una chip su un calendario pubblico deve dire la parola e
  il colore, e un visitatore non può leggere `/api/calendar-kinds`, che sta dietro `Calendar.View`.

E due difetti trovati per strada, corretti nello stesso commit: una `Select` generata non aveva
`id`, quindi l'etichetta della sua riga puntava al nulla e il campo non aveva **nessun** nome
accessibile — valeva per ogni campo `choices` di ogni form; e Playwright fa il match delle rotte in
ordine **inverso** di registrazione, per cui lo stub di `**/api/calendar**` rispondeva anche a
`/api/calendar-kinds`.

**Il colore adesso è una colonna**, e la mezza pagina che lo derivava dalla parola è sparita: un
colore scelto da qualcuno può accomunare due tipi che vanno insieme, un hash no.
