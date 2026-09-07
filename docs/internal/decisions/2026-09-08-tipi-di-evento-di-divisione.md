# I tipi di evento sono di divisione: dove metterli

**Data:** 8 settembre 2026 — G13, richiesta 11 della demo
**Stato:** ⚠️ **proposta, da decidere con Carmine.** Niente è stato scritto.
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
