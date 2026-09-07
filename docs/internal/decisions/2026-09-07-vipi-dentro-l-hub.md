# Portare vIPI dentro l'hub

**Data:** 7 settembre 2026 — chiesta da Carmine («il servizio attuale delle vIPI lo dovremo portare
dentro questo sito, organizziamoci»)
**Stato:** **decisa** (Carmine, 7 settembre 2026) — §«La decisione»
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**. Non è una funzione dell'hub: è un secondo
prodotto che entra nel processo dell'hub, e tocca runtime, database, autenticazione e deploy.

## Che cosa esiste oggi, verificato leggendo i due repository

Niente qui è ricordato: è letto il 7 settembre 2026 in
`D:\Programmazione\IVAO_Test\vIPI Ivao Italy\vIPI Ivao Italy`.

| | vIPI | Hub |
|---|---|---|
| Runtime del processo | **net8** (`Vipi.Host`) | **net10** |
| EF Core | **8** in produzione, **10** sul ramo net10 | **9.0.19** |
| Provider MariaDB | **Pomelo 8.0.3**, solo sul TFM `net8.0` | **Pomelo 9.0.0** |
| Database di produzione | **MariaDB**, schema `itivao_atc`, `Server=localhost;Port=3306` | MariaDB, stesso server |
| Dove gira | Plesk, `/var/www/vhosts/it.ivao.aero/public_atc/`, Passenger | lo stesso Plesk |
| UI | **Blazor Server**, `InteractiveServer` | SPA React, nessun Blazor |

Tre cose che cambiano il quadro **in meglio**:

1. **vIPI è già su MariaDB, sullo stesso server.** Il piano 00 lo sapeva (§2.3-bis, §13); la **guida
   del modulo** parla ancora di SQLite come default, ed è lì che si legge la cosa che conta: il
   «progetto MySQL» di `docs/design/piano-supporto-mysql.md` **è finito**. La produzione ha
   `Persistence:Provider = MySql`, un database suo (`itivao_atc`) e 48 migrazioni dedicate in
   `Vipi.Infrastructure.MySqlMigrations`.
2. **vIPI è già progettata per essere montata.** `Vipi.Hosting` espone
   `AddVipiModule` / `UseVipiModule` / `MapVipiModule` / `MigrateVipiDatabase`, e l'identità arriva
   dall'host con `HostIdentityCurrentUserProvider`: quale claim porta il VID, il nome e le posizioni
   staff è **configurazione** (`HostIdentityOptions`), non codice. Per il nostro cookie sono quattro
   righe: `vid`, `given_name`, `pos`.
3. **Blazor Server dietro Plesk funziona.** Non è una supposizione da verificare: `atc.it.ivao.aero`
   lo fa in produzione da agosto, su questo Passenger e dietro questo nginx.

## Il vero ostacolo, ed è uno solo

**Un processo ha una sola versione di EF Core**, e le due metà non si incontrano:

- l'hub è su **EF 9 + Pomelo 9** perché *Pomelo non ha una build per EF Core 10* (lo dice il commento
  in `Directory.Packages.props`, ed è la stessa ragione che tiene `Vipi.Host` su net8);
- vIPI ha il MariaDB **solo** sul ramo net8/EF8/Pomelo8, e il suo ramo net10 usa EF **10** con
  SQLite e Postgres. La guida del modulo lo scrive per esteso: *«un host net10 può montare il modulo,
  ma non con questo provider»*.

Nessuna delle tre combinazioni esistenti coincide. Ma **la combinazione che serve esiste e l'hub la
sta già esercitando da nove fasi**: `net10 + EF Core 9 + Pomelo 9.0.0` contro MariaDB 11.4.10, con
150 test di integrazione che ci girano sopra. Quello che manca non è un provider da inventare: è un
ramo di vIPI che punti a quella terna invece che a EF 10.

⚠️ È lavoro **nel repository di vIPI**, non in questo. Va aperto lì, con la sua suite (1115 test su
net8, 996 su net10) a dire se il passaggio ha rotto qualcosa.

## Le tre strade

| | Come | Costo | Che cosa si ottiene |
|---|---|---|---|
| **A. Montaggio in-process** (quello che il piano chiama M5) | vIPI guadagna il ramo net10/EF9/Pomelo9; l'hub aggiunge Blazor Server, monta la RCL e mappa `/services/vsop` | il ramo nuovo in vIPI, Blazor dentro l'hub, un solo Data Protection, un solo pool | Un processo, un dominio, **un solo login**, un solo deploy |
| **B. Due app, un dominio** (proxy su `/services/vsop`) | nginx del dominio dell'hub inoltra `/services/vsop` alla vhost `public_atc` che c'è già | nessuna riga di C#; SSO fra due cookie, due deploy | Il visitatore vede un sito solo, **subito**, senza toccare né vIPI né l'hub |
| **C. B adesso, A quando conviene** | oggi il proxy; il montaggio quando il ramo net10/EF9 di vIPI esiste ed è provato | la somma delle due, spalmata | Nessuna finestra in cui il sito è a metà |

**Raccomandazione: C, con A come destinazione.** Il montaggio è la scelta giusta e il piano l'ha
presa per ragioni che restano valide (un processo, un login, un deploy); ma è **bloccato da un
allineamento di versioni che non dipende da noi due**, e finché dura, B dà al lettore il 90 % del
risultato — un sito solo — al prezzo di una riga di configurazione nginx. Il rischio di A fatto in
fretta è quello che la scheda di vIPI dice già di sé: *«il confine di ciò che è provato è la
compilazione»*.

⚠️ **Quello a cui si rinuncia scegliendo B**, scritto perché non si scopra dopo: due login, quindi
due consensi IVAO per lo stesso utente; due pacchetti da caricare; e il menu dell'hub che porta a
`/services/vsop` senza che l'hub sappia se dietro c'è qualcuno. Sono tutti reversibili passando ad A.

## Che cosa tocca all'hub, in ogni caso

Piccolo, ed è la parte buona: `/services/vsop`, `/vsop`, `/_content` e `/_framework` sono **escluse
dal fallback della SPA da F0** (design M0 §4 punto 5). Il modulo `atc` esiste da M0 apposta. Con A si
aggiunge il montaggio e le card di `/atc` diventano voci di menu vere; con B non si tocca niente,
si scrive una riga in `cms_menu_items` — che da G8 è una tabella.

## La decisione (7 settembre 2026)

**Strada C: il proxy adesso, il montaggio in-process come destinazione.** Il visitatore vede un sito
solo appena il dominio dell'hub è in piedi, senza che una riga di C# cambi da nessuna delle due
parti; il montaggio arriva quando vIPI ha il ramo `net10 + EF 9 + Pomelo 9` **provato dalla sua
suite**, non compilato e basta.

**Resta M5**, dopo Events, Tour e Training, come dice §13: i tre moduli di dipartimento stanno davanti
al lavoro più rischioso. Le due righe del piano che dicevano M4 (§2.3 e §9.5) erano il refuso, e sono
corrette.

Conseguenze pratiche, in ordine di quando servono:

1. **Niente da fare nell'hub oggi.** Le rotte di vIPI sono escluse dal fallback della SPA da F0 e il
   modulo `atc` esiste. Quando il dominio dell'hub sarà davanti, `/services/vsop` è una riga di nginx
   e una voce in `cms_menu_items`.
2. **Il ramo net10/EF9/Pomelo9 è lavoro nel repository di vIPI**, e va aperto lì con la sua suite a
   fare da rete. Non è nel perimetro di nessuna fase di M1.
3. **M5 riceve un documento di design suo** quando si apre, come ogni altro modulo: il montaggio
   tocca autenticazione (il login di vIPI sparisce, l'identità arriva dal cookie dell'hub per
   mappatura di claim), Data Protection, pool di connessioni, Blazor Server dentro un host che oggi
   non ne ha, e la CSP per le tessere della mappa.

⚠️ **Il nodo non dipende da noi**: se Pomelo pubblicasse un provider per EF Core 10, la terna
diventerebbe `net10 + EF 10 + Pomelo 10` per tutti e due i prodotti e il ramo intermedio non
servirebbe più. Vale la pena guardare quel repository prima di aprire il lavoro.
