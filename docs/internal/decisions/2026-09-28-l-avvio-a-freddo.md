# L'avvio a freddo: dove vanno i secondi e che cosa li taglia

**Data:** 28 settembre 2026
**Stato:** **Proposta**. Le domande sono nel §7 e vanno a Carmine come commento sulla pull request. Nessun codice di
produzione in questa nota: il codice arriva in una PR del nucleo dopo la sua risposta, con il salto di versione secondo la
regola di `Directory.Build.props`.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: ogni taglio proposto estende un meccanismo che c'è già (il pacchetto
self-contained, `InitializeAsync`, `hub_jobs_log`, `diagnostics/starts.txt` deciso con la #165). Nessun meccanismo nuovo.
**Da dove viene:** la misura della #165 (nota `2026-09-28-i-job-quando-passenger-spegne-l-hub` §5) e la decisione della #164
(nota `2026-09-28-gli-header-dei-file-statici` §3, strada A), che aspetta questa nota prima del suo codice.

## 1. Il problema, in numeri del server

Su `test.it.ivao.aero` (`0.2.1`, `fa089de`) Passenger spegne l'hub dopo **10–30 s** di inattività. Dopo almeno 60 s di
silenzio **ogni GET ha pagato 7,8–10,0 s** (7 su 7, mediana 8,4 s), contro 0,2 s a processo vivo. Né il tempo
d'inattività né `passenger_min_instances` sono nelle mani della divisione (Carmine, 28 set), e su vIPI un ping ogni 10 s
«riaccende, non tiene su» (nota della #165, §2). Quindi **l'avvio a freddo lo paga il visitatore**, spesso: la domanda è
quanto se ne può togliere dal codice e dal pacchetto.

## 2. Come si è misurato

- **Il pacchetto vero**: `dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained` nel contenitore
  `mcr.microsoft.com/dotnet/sdk:10.0` (SDK 10.0.401), dal codice di `main` (`6c20b22`, lo stesso codice di `fa089de` più una
  nota), con la SPA in `wwwroot/`.
- **Avviato come Passenger**: `dotnet IvaoHub.Web.dll --urls …` con la cartella dell'app come cartella di lavoro, da un host
  `dotnet` **8.0.31** (l'immagine `aspnet:8.0`, la stessa versione misurata sul server il 27 set), `ASPNETCORE_ENVIRONMENT=Production`,
  contro **MariaDB 11.4.10** in un altro contenitore, con il database **già migrato e già seminato**: il caso del server,
  niente da migrare. Due centri in `ref_ivao_centers`, perché il controllo dei dati di riferimento non chiami IVAO.
- **Da fuori**, come la vede Passenger: dall'avvio del processo al primo `GET /` risposto (la porta apre solo dopo
  `InitializeAsync`), poi `GET /api/me` anonimo, `GET /api/content/public/page`, `GET /health`.
- **Da dentro**, una sonda **temporanea e mai committata**: una riga con i millisecondi dall'avvio del processo a ogni passo
  di `Program.cs` e di `InitializeAsync`, e all'entrata e all'uscita della prima richiesta. Le varianti «salta questo passo»
  erano interruttori della sola sonda.
- **Ogni variante 5–7 avvii**, ognuno in un contenitore nuovo; nelle tabelle c'è la **mediana**.
- **La CPU**: la macchina di sviluppo ha 32 thread. Il server è un hosting condiviso di cui non sappiamo i core né il
  carico, e con ogni probabilità è più lento: per questo la misura principale è con **una CPU sola** (`--cpus=1`), e anche
  così il server resta **circa 2,7 volte più lento** (§5). Le misure con tutte le CPU sono nel §4.4.
- **La cache del disco**: di norma calda (i file sono già in memoria dall'avvio prima). Una serie a parte la svuota prima
  di ogni avvio (`drop_caches`), per il caso di un server condiviso che ha dovuto liberare memoria.

Gli script e la sonda non entrano nel repository: la misura che resta è quella del server, in `diagnostics/starts.txt`
(§6, punto 4).

## 3. Dove vanno i secondi, oggi

Pacchetto di oggi, una CPU, cache calda, mediana di 7 avvii: **primo `/` risposto a 2,93 s, `/api/me` a 3,03 s.**

| Passo | ms | |
|---|---:|---|
| Runtime e host fino a `Main` | 25 | |
| `CreateBuilder`, registrazione dei servizi, `Build` | 176 | |
| Pipeline ed endpoint mappati | 84 | |
| `IStartupValidator` | 12 | |
| **Modello EF di `HubDbContext`** | **567** | costruito per `VerifyAlternatives`; servirebbe comunque alla prima query |
| Modello EF di `FlightOpsDbContext`, di `TrainingDbContext` | 203 + 90 | |
| Migrazioni del nucleo: `GetPendingMigrationsAsync` | 222 | nessuna migrazione da applicare |
| **Migrazioni dei moduli: `MigrateAsync` sempre** | **206 + 58** | e in quattro avvii su dieci **uno stallo di 0,6–1,0 s** (sotto) |
| `SuperadminService.BootstrapAsync` | 199 | |
| `PositionGrantSeeder` | 9 | |
| **`ContentSeeder`** | **279** | |
| Controllo dei dati di riferimento, `StartupDiagnostics` | 10 | |
| Kestrel e Quartz, fino a `ApplicationStarted` | ~100–170 | |
| **Prima richiesta: la tabella delle rotte** | **417** | la costruisce ASP.NET alla prima richiesta, prima di ogni middleware |
| La prima richiesta in sé | 27 | |

Raggruppato: **avvio di .NET e dell'host ~0,3 s** (10%), **modelli EF ~0,86 s** (30%), **lavoro di inizializzazione sul
database ~1,0 s** (33%), Kestrel e Quartz ~0,15 s, **tabella delle rotte ~0,42 s** (14%).

- **È CPU, non database.** Un avvio apre ~24 connessioni e manda qualche decina di comandi, ognuno sotto il millisecondo
  lato server. I secondi sono il JIT e la costruzione dei modelli e delle query di EF. Sul MariaDB condiviso ogni andata e
  ritorno costa di più che fra due contenitori: *non misurato*, ma con qualche decina di comandi vale decimi, non secondi.
- **Lo stallo nelle migrazioni dei moduli.** Il nucleo chiede prima `GetPendingMigrationsAsync` e chiama `MigrateAsync` solo se
  c'è qualcosa (`HubDatabaseInitializer`); i moduli chiamano **sempre** `MigrateAsync` (`HubPipeline.cs:185-189`). Con EF 9 e
  Pomelo 9 quella chiamata, anche senza niente da fare, prende `GET_LOCK('__hub_EFMigrationsLock', 259200)`, esegue
  `CREATE TABLE IF NOT EXISTS` della tabella della storia, apre una transazione e poi `RELEASE_LOCK`. Con una CPU, in **34
  avvii su 88** il `RELEASE_LOCK` di Training è rimasto **0,6–1,0 s** «in esecuzione» lato client, mentre lato server la
  stessa sequenza costa **0,1 ms** (misurato a mano su MariaDB). Con tutte le CPU non è successo mai (0 su 24). È quindi CPU
  contesa dentro il processo, probabilmente il lavoro in background del runtime, non il database; la causa esatta non l'ho
  isolata. Con il controllo del nucleo usato anche per i moduli lo stallo **non si è più visto** (0 su 47 avvii).
- **La tabella delle rotte.** Verificato spostando `UseRouting` dopo la sonda: i ~0,4 s passano dentro la prima richiesta.
  Sono gli endpoint minimal API, i cui delegati ASP.NET compila alla prima richiesta.

## 4. I tagli, misurati da soli e insieme

### 4.1 La tabella

Una CPU, cache calda, mediane; «`/api/me`» è quando la home può mostrare qualcosa.

| Variante | primo `/` | `/api/me` risposto | rispetto a oggi |
|---|---:|---:|---:|
| **Oggi** | 2,93 s | **3,03 s** | — |
| TieredPGO spento | 2,58 s | 2,65 s | −0,37 s |
| Migrazioni dei moduli solo se pendenti | 2,40 s | 2,47 s | −0,56 s |
| Marcatore d'inizializzazione (§4.3), senza la sua lettura | 1,72 s | 2,10 s | −0,92 s |
| **ReadyToRun** | 1,97 s | 2,02 s | −1,00 s |
| ReadyToRun + migrazioni dei moduli | 1,80 s | 1,86 s | −1,17 s |
| **ReadyToRun + migrazioni dei moduli + TieredPGO spento** | 1,70 s | **1,75 s** | **−1,28 s (−42%)** |
| ReadyToRun + marcatore + TieredPGO spento | 1,20 s | 1,40 s | −1,63 s (−54%) |
| … + Request Delegate Generator | 1,11 s | 1,30 s | −1,73 s (−57%) |

A cache fredda: oggi **3,10 s**; ReadyToRun + moduli + PGO spento **2,05 s**; con il marcatore **1,75 s**. La cache
fredda costa di più a ReadyToRun (i suoi file sono più grandi da leggere: +0,3 s nella combinazione, contro +0,07 s del
pacchetto di oggi), ma la combinazione resta avanti di un secondo.

### 4.2 Ogni taglio, con il suo costo

**ReadyToRun** (`PublishReadyToRun`): compila in anticipo il codice nostro e delle librerie (EF, Pomelo, Quartz, Serilog…);
il framework .NET è già precompilato nel runtime pack. **−1,0 s**, il taglio più grande.
- Pacchetto: **139 → 172 MB** su disco, **59 → 73 MB** compresso.
- **Deterministico**: due build ReadyToRun di codice diverso differiscono solo nelle **quattro DLL nostre**. La prima consegna
  con ReadyToRun cambia **53 DLL** (+14 MB nello zip), una volta; da lì le consegne a pochi file di
  `tools/prepare-delivery.ps1` restano piccole come oggi. Lo script lavora per hash e non va toccato.
- Nel contenitore la pubblicazione è durata 17–24 s contro 10–29 s: nessuna differenza apprezzabile.
- ⚠️ Va condizionato al RID (`Condition="'$(RuntimeIdentifier)' != ''"`): il banco e2e pubblica **senza** `-r`, e
  ReadyToRun vuole un RID. *Non provato*: la pubblicazione ReadyToRun per linux-x64 da Windows (crossgen la
  supporta; la CI e il rilascio pubblicano da Linux).

**Le migrazioni dei moduli solo se pendenti**: lo stesso controllo che il nucleo fa già, esteso ai contesti dei moduli.
**−0,17 s** di lavoro più lo stallo (fino a −1,0 s in quattro avvii su dieci). Una correzione di poche righe in `HubPipeline`, senza
effetto sul comportamento: se c'è una migrazione, si applica come oggi.

**TieredPGO spento** (`<TieredPGO>false</TieredPGO>`, finisce in `runtimeconfig.json`): il JIT non strumenta i metodi per
ottimizzarli meglio dopo. **−0,1 s** sopra ReadyToRun (−0,37 s senza); lo stallo del §3 non lo toglie. Il costo è qualche percento di
velocità a regime sulle strade calde: per il traffico di una divisione, irrilevante.

**Il marcatore d'inizializzazione** (§4.3): **−0,36 s** con ReadyToRun (−0,9 s senza), ma è una **stima per eccesso**: la
variante misurata salta i passi senza leggere il marcatore, e la lettura costa una query. Parte del guadagno la riprende la
prima richiesta, che compila le prime query EF (`/api/me` passa da 45 a ~190 ms).

**Request Delegate Generator** (`EnableRequestDelegateGenerator`): genera in compilazione i delegati degli endpoint.
**−0,1 s** sulla tabella delle rotte (417 → ~265 ms). Cinque endpoint di `MapCrud` restano a runtime (avviso `RDG002`), e con
`TreatWarningsAsErrors` la build si ferma finché non si silenziano. **Non raccomandato ora**: poco guadagno, un meccanismo in
più nella build; si può riprendere dopo la misura del server.

**Scartati, con la misura:**
- **Modelli EF compilati** (`dotnet ef dbcontext optimize`): **impossibili** con EF 9 in questo repository. Il comando si
  rifiuta per due ragioni: le funzioni con traduzione propria (`LocalizedQuery`, `JsonQuery`) e, tolte quelle per prova, i
  **filtri globali di query** («query filters are not supported»). Il filtro di visibilità è uno dei meccanismi del nucleo
  (`CLAUDE.md` §2): non si toglie per l'avvio. E Pomelo 10 non esiste.
- **GC workstation invece di server**: nessuna differenza (2,15 s contro 2,16 s con 32 CPU; memoria 205 MB contro 217 MB).
  .NET 10 adatta il numero di heap da sé.
- **Scaldare l'hub da solo prima di aprire la porta**: sposta i secondi, non li toglie; Passenger tiene la richiesta finché
  la porta non apre.

### 4.3 Il marcatore: che cosa può saltare e che cosa no

L'idea: l'inizializzazione completa gira quando qualcosa è cambiato; un semplice risveglio dello stesso pacchetto sullo stesso
database non rifà controlli già fatti.

- **Dove sta**: nel **database**, come una riga di `hub_jobs_log` (job `startup`, esito `ok`, nel messaggio la chiave). Niente
  tabella nuova, niente migrazione. Non in `diagnostics/`: un database ripristinato da un backup vecchio con un file che dice
  «già fatto» farebbe girare il codice su uno schema non migrato. Nel database, il backup vecchio porta con sé il suo
  marcatore vecchio, o nessuno.
- **La chiave**: versione e commit del pacchetto, l'hash di `config/division.json` e dei file di `seed/`, i moduli abilitati.
  Cambia uno di questi, cambia la chiave, e l'avvio fa tutto.
- **Si scrive per ultimo**, dopo tutti i passi riusciti. Un crash a metà non lascia marcatore, e l'avvio dopo rifà tutto:
  ogni passo è già idempotente («applicato una volta sola»). Due processi insieme (la #165 li ha visti) fanno tutti e due
  l'inizializzazione intera, come oggi.
- **Gira sempre**: la validazione della configurazione, la costruzione dei modelli con `VerifyAlternatives` (il modello del
  nucleo serve comunque alla prima query), il controllo dei dati di riferimento vuoti (costa ~8 ms, e se la prima
  sincronizzazione è fallita deve riprovare), `StartupDiagnostics`, e la lettura del marcatore.
- **Salta, se la chiave è quella**: il controllo delle migrazioni (nucleo e moduli), `SuperadminService`, `PositionGrantSeeder`,
  `ContentSeeder`.
- **Il rischio vero** è un cambiamento fatto fuori dal pacchetto e fuori dalla chiave che l'inizializzazione avrebbe dovuto
  vedere. Oggi non ne conosco uno; nella PR vanno elencati gli ingressi di ogni passo saltato, uno per uno, con un test.

## 5. Che cosa vuol dire per il server

Il server ha pagato **8,4 s** (mediana) dove questa misura, con una CPU, ne paga **3,03**: circa **2,7 volte** più lento, tolti
i ~0,2 s di rete che ha anche una richiesta a processo vivo. Se il rapporto regge per tutti i passi (il lavoro è quasi tutto
CPU, §3), sul server (tempo qui × 2,7, più 0,2 s di rete):

| | qui, una CPU | sul server, stimato |
|---|---:|---:|
| Oggi | 3,0 s | **8,4 s** (misurato) |
| ReadyToRun + migrazioni dei moduli + TieredPGO spento | 1,75 s | **~4,9 s** |
| … + marcatore | 1,4 s | **~4,0 s** |
| … + Request Delegate Generator | 1,3 s | ~3,7 s |

⚠️ **È una stima**, non una misura: non sappiamo quanti core ha il server, se è carico, quanto conta la cache del disco né
quanto lo spawn di Passenger. Lo dirà `diagnostics/starts.txt` (§6, punto 4).

**La conclusione onesta**: dal codice e dal pacchetto si può togliere **circa metà** del risveglio. **I 2–3 s non si
raggiungono** su quel server, con quello che si può cambiare da qui. L'unico modo di non pagare l'avvio è che il processo
resti vivo, cioè l'host (la domanda a Ivao.It della #165).

**Per la strada A della #164** (l'`index.html` servito solo dall'hub):
- **Oggi** il web server dà subito `index.html` e gli asset. Il visitatore vede la cornice della SPA in qualche decimo di
  secondo, poi aspetta `/api/me`, che paga il risveglio. Il risveglio però **comincia solo quando parte `/api/me`**, qualche
  decimo di secondo dopo.
- **Con A** il risveglio comincia alla prima GET, e il visitatore vede **una pagina bianca** per tutto il risveglio.
- **Il tempo fino al contenuto è quasi lo stesso** nei due casi. Cambia che cosa si vede mentre si aspetta: la cornice
  dell'hub, o il bianco del browser.
- Con i tagli, quel bianco sarebbe di **~4–5 s** invece di 8–10. Sotto i 3 s una pagina bianca è un caricamento lento; sopra,
  per molti è un sito rotto.

## 6. La raccomandazione

1. **Una PR del nucleo con i tagli a buon mercato**:
   - ReadyToRun, condizionato al RID;
   - le migrazioni dei moduli solo se pendenti;
   - TieredPGO spento.

   Nessuna migrazione, nessuna pagina: **PATCH**. La prima consegna porta le 53 DLL cambiate. Stima: da 8,4 a ~4,9 s sul
   server.
2. **Il marcatore d'inizializzazione in una PR sua, dopo**. È codice con un rischio (§4.3) per ~1 s stimato sul server:
   meglio deciderlo con in mano i numeri del server del punto 4.
3. **La strada A della #164 aspetta la misura del server dopo il punto 1.** Se il risveglio mediano in `starts.txt` sta
   sotto i ~3 s, A come deciso; se resta sui 4–5 s, torna a Carmine (§7, domanda 3).
4. **`diagnostics/starts.txt` (deciso con la #165) scrive anche la durata dei passi dell'avvio**: modelli, migrazioni,
   seeder, avvio di Kestrel. Così la stima del §5 diventa una misura del server, e ogni taglio si verifica dove conta. Sono
   poche righe dentro un file già deciso.
5. **Scartati**: modelli EF compilati (impossibili con i filtri globali), GC workstation (nessun effetto), il Request
   Delegate Generator per ora (§4.2).

## 7. Le domande a Carmine

1. **La PR del nucleo con ReadyToRun, le migrazioni dei moduli solo se pendenti e TieredPGO spento?** Raccomandato: **sì**,
   PATCH. Il costo: il pacchetto passa da 59 a 73 MB compresso, e la prima consegna cambia 53 DLL invece di 4.
2. **Il marcatore d'inizializzazione?** Raccomandato: **sì, ma dopo**, in una PR sua, deciso con i numeri di `starts.txt` sul
   server dopo la domanda 1.
3. **La strada A della #164**, se dopo i tagli il server si sveglia ancora in 4–5 s: A resta decisa (la CSP sul documento vale
   una pagina bianca di 4–5 s), o si riapre? Raccomandato: **decidere con la misura del server in mano**, non prima. Se sta
   sotto i 3 s, A senza discussione.
4. **La durata dei passi dell'avvio in `diagnostics/starts.txt`?** Raccomandato: **sì**, nella PR di `starts.txt` della #165
   o nella PR della domanda 1, quella che arriva prima.

## Da portare nel piano

- **§2.5**, la riga di Passenger: l'avvio a freddo misurato (8–10 s sul server), che cosa lo compone (§3) e che cosa lo
  taglia (§4), e che i 2–3 s non si raggiungono dal codice (§5).
- **§11.3 punto 1** (Pacchetto): ReadyToRun, se deciso (domanda 1), con dimensioni e determinismo.
- **§11.3 punto 5** (Migrazioni): i moduli controllano le migrazioni pendenti come il nucleo; il marcatore, se deciso.
- **§11.3 punto 2**: la durata dei passi dell'avvio in `diagnostics/starts.txt` (domanda 4).
- **`docs/DEPLOYING.md`**, «Known limits»: il costo di un risveglio, quando i tagli sono nel codice.
