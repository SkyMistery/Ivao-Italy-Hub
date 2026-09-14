# I dati condivisi con vIPI: due database, un padrone per ogni dato, letture incrociate

**Data:** 14 settembre 2026 — domanda di Carmine («vIPI diventerà di fatto l'estensione di questo sito:
c'è un modo per unire i due DB o comunque non duplicare i dati? Ci conviene anche per la sicurezza?»)
**Stato:** **decisa** (Carmine, 14 settembre 2026) su §3.1–§3.5; restano **da verificare sul server** le
due cose di §4 prima del primo codice che le usa.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: tocca il perimetro con un prodotto esterno, il
database e i permessi del server.
**Riapre in parte** `2026-09-13-staccarsi-da-vipi.md`: lì «nessuna API di vIPI è consumata dall'hub». Resta
vero che l'hub **non monta** vIPI e non ne usa le API; da qui l'hub **legge dati di vIPI** attraverso viste
del database, e vIPI in futuro leggerà dati dell'hub allo stesso modo. M5 (il montaggio) resta sospeso.

## 1. Che cosa serve

- vIPI (`atc.it.ivao.aero`) resta un'app separata, ma è **l'estensione ATC dell'hub**: i due siti devono
  usare gli stessi dati **senza copiarli**.
- Il primo consumatore è il modulo dei tour: il controllo della **copertura ATC** di una leg legge
  l'archivio delle sessioni ATC che vIPI già tiene (`AtcSession`). Poi l'Online Day (ED) per il territorio.

## 2. Che cosa esiste, verificato leggendo i due repository il 14 settembre 2026

- **Stesso server MariaDB 11.4.10, due database**: vIPI in `itivao_atc`, l'hub nel suo. Pool condiviso (§2.5).
- **Tecnologie diverse**: vIPI in produzione è net8 + EF Core 8 + Pomelo 8; l'hub net10 + EF Core 9 +
  Pomelo 9. Le due app non possono condividere codice di accesso ai dati, ma leggono lo stesso server.
- **Dati che si sovrappongono**:

  | Dato | In vIPI | Nell'hub |
  |---|---|---|
  | Staff e permessi | `StaffMember`, `RoleOverride` | `hub_users`, posizioni, grant |
  | Audit, media, documenti | sì | sì |
  | Aeroporti | curati a mano (piste, SID, livelli di transizione) | snapshot dall'API IVAO (`ref_ivao_*`) |
  | Archivio delle sessioni ATC | `AtcSession`, traffico, piste, riepiloghi mensili (anche fuori divisione dal 28 ago 2026) | — |
  | Settori e loro forme | `Sector`, `Acc`, `SectorShapePart`, `AirspaceVolume` | — |

## 3. Le decisioni

### 3.1 Due database, non uno (Claude, accettato da Carmine)

- **Un database unico con un utente unico è scartato**: una falla in un'app esporrebbe i dati dell'altra
  (nell'hub: token IVAO cifrati, grant, registri disciplinari dei piloti); due app con versioni diverse di
  EF migrerebbero lo stesso schema e andrebbero rilasciate sempre insieme; backup e ripristino non
  sarebbero più separabili.
- **Un database unico con due utenti** è scartato per le ultime due ragioni.

### 3.2 Ogni insieme di dati ha un solo padrone, che è l'unico a scriverlo

- **L'hub** è padrone di: persone e login, staff, permessi e grant, contenuti editoriali, tour, training,
  eventi, award, notifiche.
- **vIPI** è padrone di: dati operativi ATC — aeroporti curati, settori e forme, SOP e LoA, archivio delle
  sessioni ATC.
- **Mai scritture incrociate.** Chi non è padrone legge e basta.

### 3.3 Si legge attraverso viste di sola lettura, che sono il contratto

- Il padrone espone **viste** con un prefisso `v_share_` (per esempio `itivao_atc.v_share_atc_sessions`),
  create dalle sue migrazioni. Le viste sono il **contratto**: il padrone può cambiare le tabelle sotto,
  ma una vista cambia solo in modo additivo (colonne in più), come le migrazioni (piano §11.3).
- L'altra app legge con un **utente MariaDB suo e dedicato**, con solo `SELECT` su quelle viste e un tetto
  di connessioni proprio. È **più sicuro di oggi**: una falla in un'app legge, al massimo, le viste
  condivise dell'altra.
- Nell'hub la lettura passa da un **contesto EF di sola lettura** nel nucleo, con la sua connessione:
  nessuna FK, nessun join fra i due database, nessuna migrazione (le viste le crea vIPI). Stesso server,
  quindi nessuna latenza e nessuna API da mantenere.
- **Scartata l'API HTTP** fra le due app come strada principale: più codice da tutte e due le parti, e la
  disponibilità di un'app diventerebbe quella dell'altra per ogni lettura. Resta il **ripiego** se §4.1
  dicesse che il server non permette l'utente dedicato.

### 3.4 L'hub funziona anche senza vIPI (forkabilità)

- Una divisione che forka l'hub **non ha vIPI**. La lettura delle viste è un'**integrazione opzionale**
  del nucleo, accesa da `config/division.json` (per esempio `atcData: { "source": "vipi" }`, con la
  stringa di connessione nei segreti), dietro un'interfaccia del nucleo; spenta, l'hub non la cerca.
- **Nessun modulo nomina vIPI**: vale la stessa domanda di «questo nomina IVAO?» (piano §4.2). Il modulo dei
  tour chiede al nucleo «chi controllava questa posizione in questo intervallo»; senza vIPI il nucleo
  risponde con i dati IVAO (sessioni del tracker) o dichiara il controllo **non disponibile** — mai
  «fallito» (Toursystem ADR-011).

### 3.5 Lo staff di vIPI, a regime

- vIPI smetterà di tenere `StaffMember` e `RoleOverride` e leggerà una vista dell'hub (staff, posizioni,
  grant). È **lavoro nel repository di vIPI**, non ha data, e non blocca niente dell'hub.

## 4. Da verificare sul server prima del primo codice

1. **L'utente MariaDB dedicato**: la sottoscrizione Plesk crea utenti dal pannello con privilegi non
   verificati (§2.5). Serve sapere se si può avere un utente con `SELECT` solo sulle viste `v_share_` di
   `itivao_atc` (un `GRANT` fatto da chi amministra il server). Se no, ripiego sull'API HTTP di sola
   lettura (§3.3).
2. **Le viste con `SQL SECURITY DEFINER`**, create dalle migrazioni di vIPI: che l'utente di vIPI abbia il
   privilegio di crearle.

## 5. Che cosa si tocca

- **Piano**: §2.5 (una riga), §9.7 (i contratti con i prodotti esterni), §15 punto 2, il changelog 0.78.
- **`2026-09-13-staccarsi-da-vipi.md`**: una riga di stato che rimanda qui.
- **`CLAUDE.md`** §8.
- **Codice, quando serve**: nel design dei tour (M2), il controllo della copertura ATC dichiara che cosa
  legge; la vista `v_share_atc_sessions` la crea una migrazione in vIPI. Il contesto di sola lettura e
  l'interfaccia del nucleo nascono con quella fase, non prima.
