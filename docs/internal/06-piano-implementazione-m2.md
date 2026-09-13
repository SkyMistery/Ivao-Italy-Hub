# IVAO Division Hub — Piano di implementazione di M2

> Documento **interno** (italiano). Fonte di verità: `00-piano-di-progettazione.md` (§13, M2).
> Si scrive in due parti. La **parte A** sono i prerequisiti che il piano mette «prima di tutto»:
> i moduli fuori dai dipartimenti (`decisions/2026-09-13-moduli-non-subordinati-ai-dipartimenti.md`
> §4). La **parte B** — il modulo Events — si scrive dopo la nota sulle due dashboard personali e
> `05-design-m2.md`, come dice il piano, e non c'è ancora.

## A. I prerequisiti: i moduli fuori dai dipartimenti

Tre fasi, una PR ciascuna, da `main`, nell'ordine. Nessuna tocca i contenuti editoriali: una pagina,
una news, un documento restano di un dipartimento solo (nota §3.3).

| Fase | Titolo | Dipende da | In una riga |
|---|---|---|---|
| H1 | I grant a una posizione — **fatta il 13 set 2026** | — | il soggetto di un grant è un VID **oppure** un dipartimento con uno o più livelli; seed una volta da `division.json` |
| H2 | Le righe di più dipartimenti | H1 | `IOwnedByDepartment` a insieme nel filtro, nel handler, nell'interceptor e nelle liste; `modules.<key>.baseDepartment`; un modulo di prova con una tabella nei test |
| H3 | Le sezioni dei moduli nella barra dello staff | H2 | Contenuti · un gruppo per modulo · Dipartimenti · Amministrazione |

### H1 — I grant a una posizione

Nota §3.2. Branch `m2/h1-position-grants`.

**Deciso con Carmine il 13 settembre 2026**: la posizione si indica con **dipartimento + uno o più
livelli** (coordinator, assistant, advisor, membro), non con il codice della posizione. Chi arriva a
quel ruolo prende il permesso da solo e chi lo lascia lo perde; regge quando IVAO rinumera le
posizioni.

1. **Il soggetto.** Sulla stessa tabella `hub_user_grants`: `vid` diventa facoltativo, e si
   aggiungono `position_department` e `position_levels_json` (l'elenco dei livelli). Un grant ha
   **esattamente uno** dei due soggetti. Stessi `value`, `department` (lo scope: `null` = ogni
   dipartimento), `effect`, `expires_at`, `reason`, stesso audit. Migrazione additiva.
2. **I permessi effettivi.** `EffectivePermissionsCalculator` riceve i grant del VID e quelli di
   posizione; un grant di posizione vale per chi ha **almeno una posizione** con quel dipartimento e
   uno di quei livelli. Le regole restano una: niente permessi globali da un grant (quindi mai
   `Permissions.Manage` né il superadmin), deny che vince, scadenza. **La sospensione** resta dei
   grant a un VID (chi perde ogni posizione): un grant di posizione non ha nessuno da sospendere, smette
   di valere perché nessuno ha più la posizione.
3. **Le sessioni.** Scrivere un grant di posizione rinfresca la sessione di **chi ha quella
   posizione** adesso, nella stessa transazione; e un grant modificato rinfresca anche chi aveva il
   soggetto di prima. `IAffectsUserSession` si allarga alla posizione invece di aggiungere un secondo
   meccanismo.
4. **Il seed.** `division.json → positionGrants: [{ department, levels, permission, scope, deny }]`,
   letto **una volta** (una riga in `hub_division_settings` ricorda che è stato applicato), poi la verità
   è la tabella e si cambia dalla schermata. Validato all'avvio come il resto di `division.json`.
5. **La schermata.** `/staff/admin/permissions`: il form chiede prima «a chi» (un VID o una posizione)
   e poi disegna i campi di quel soggetto; la lista mostra il soggetto in una colonna sola.
6. **I test.** Unit: un grant di posizione vale per chi ha il livello, non per gli altri livelli né per
   un altro dipartimento; non conferisce un permesso globale; un deny di posizione morde. Integrazione:
   un grant di posizione arriva alla richiesta dopo, per chi ha la posizione, e la sua rimozione a quella
   dopo ancora; il seed si applica una volta sola.

**Criteri**: «AOD, coordinator e assistant → `Links.Edit` su ED» dato dalla schermata vale per un
coordinator AOD senza rifare il login e non per un membro AOD; tolto, smette di valere; con
`positionGrants` nel file, il primo avvio crea le righe e il secondo non le ricrea anche se nel frattempo
le si è cancellate.

**Fatta il 13 settembre 2026** (branch `m2/h1-position-grants`). Com'è andata:

- **Il soggetto** sta sulla riga come da punto 1: `vid` facoltativo, `position_department`,
  `position_levels_json` (letto e scritto dall'entità come elenco, come le raccolte di G20).
  `UserGrant.IsHeldThrough(positions)` è l'unica risposta a «questo grant di posizione vale per chi ha
  queste posizioni?», e la usano il calcolo dei permessi e la ricerca di chi può approvare una pagina
  (G19), che ora trova anche chi ha `Content.Approve` per posizione. Migrazione `AddPositionGrants`
  (additiva: `vid` diventa facoltativo, due colonne e un indice).
- **Il validatore**: esattamente un soggetto (`errors.grant.subject` sul campo `vid`), almeno un livello
  per una posizione (`errors.grant.levelsRequired`); il controllo «il VID è staff» vale solo per un grant
  a un membro.
- **Le sessioni**: `IAffectsUserSession` ha un secondo membro con un default, `AffectedPosition`;
  l'interceptor raccoglie le posizioni e, dopo la scrittura e nella stessa transazione, le traduce nei
  VID che le hanno. In più, **una riga modificata rinfresca anche il soggetto di prima** (i valori
  originali), cosa che per i grant a un VID prima non succedeva: spostare un grant da un membro a un
  altro lasciava al primo il cookie vecchio fino al login successivo.
- **Il seed**: `PositionGrantSeeder`, chiamato all'avvio dopo i superadmin; la riga
  `positionGrants.seeded` in `hub_division_settings` ricorda che è stato applicato. Salta con un avviso
  un permesso sconosciuto o globale. `division.example.json` e `FORKING.md` lo spiegano;
  `division.json` di IT non ne ha ancora, perché non c'è ancora un permesso di modulo da dare.
- ⚠️ **Scostamento sul punto 5**: il form **non** chiede prima «a chi» per poi disegnare i campi di quel
  soggetto. Il generatore di form non ha campi condizionali, e aggiungerli sarebbe un'estensione per un
  form solo: i due soggetti stanno uno sotto l'altro, con un suggerimento per ciascuno, e il server
  risponde sul campo quando se ne compila nessuno o tutti e due. La lista mostra VID, dipartimento e
  livelli in tre colonne.
- **I test**: tre unit (vale per i livelli giusti e non per gli altri né per un altro dipartimento; mai
  un permesso globale; un divieto di posizione morde), tre d'integrazione (il grant di posizione arriva
  alla richiesta dopo per chi ha la posizione e la rimozione a quella dopo ancora; un soggetto solo; il
  seed applicato una volta e non ricreato dopo una cancellazione), due Vitest sul payload.
- **Verificato in locale**: unit .NET (312), Vitest (389), smoke (77), lint, typecheck, formato, i18n.
  **Non in locale**: integrazione (Docker spento), che esegue la CI.

### H2 — Le righe di più dipartimenti

Nota §3.3. Branch `m2/h2-owned-by-several`. Da scrivere in dettaglio all'inizio della fase, con il
codice davanti: la forma in cui una tabella di modulo tiene l'insieme (colonna o tabella di collegamento),
come il filtro globale e la narrowing delle liste dicono «almeno uno in comune», e la forma nuova di
`modules` in `division.json` (oggi solo acceso/spento) con `baseDepartment`.

### H3 — Le sezioni dei moduli nella barra dello staff

Nota §3.1. Branch `m2/h3-module-sections`. Da scrivere all'inizio della fase.
