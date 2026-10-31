# Il MySqlConnector corretto

**Data:** 29 ottobre 2026, fase di test.
**Stato:** **decisa** (il master, su incarico di Carmine per le correzioni del nucleo, in chat alla sessione di lavoro del 29 ottobre
2026: «Yes, you take it, now»), nella forma della nota `2026-10-15-il-pkcs-corretto`: un piccolo pull request del nucleo, che entra
per primo, fissa il pacchetto con la gestione centrale dei pacchetti **senza** zittire l'audit, e alza la versione di PATCH.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**, **Correzione**: la gestione centrale delle versioni (`Directory.Packages.props`) e
l'audit di NuGet con gli avvisi come errori esistono già; si usano come sono. Nessun meccanismo nuovo.

## 1. Che cosa

- `src/IvaoHub.Core/IvaoHub.Core.csproj`: una `PackageReference` diretta a `MySqlConnector`, con un commento che dice perché c'è. Il
  riferimento sale a ogni progetto sopra il nucleo (Web, i tre moduli, i due progetti di test): tutti risolvono **2.6.2**.
- `Directory.Packages.props`: **non cambia**. La `PackageVersion` di `MySqlConnector` a 2.6.2 c'era già dal bootstrap (F0), ma nessun
  progetto la referenziava, quindi non contava: valeva la dipendenza di Pomelo.
- `Directory.Build.props`: `<Version>` da 0.14.0 a **0.14.1**, PATCH per la regola scritta accanto: solo una correzione, nessuna
  migrazione, nessuna pagina o sezione nuova.

## 2. Perché

Dal 28 ottobre 2026 (19:49 UTC) l'avviso [GHSA-473q-m89c-ghf8][g] — **moderato** — su `MySqlConnector` < **2.6.1** fa fallire
`dotnet restore` (NU1902, avviso trattato come errore) su ogni progetto, e con lui `build-test` di ogni PR e di `main`. Con `SslMode`
a `VerifyCA` o `VerifyFull` e **senza** `SslCa`, il driver può mandare la password in chiaro a un proxy in mezzo prima di aver controllato
il certificato (il TLS «a configurazione zero» di MariaDB). Non sono colpiti `SslCa` configurato, le connessioni locali, e `Preferred` o
`Required`.

Il pacchetto arriva dal nucleo per due strade: Pomelo 9.0.0 lo chiede **≥ 2.4.0** e NuGet prende la minima, 2.4.0; e il codice del
nucleo lo **nomina** (`MySqlException`, i lock dei job e dell'avvio: `JobLocks`, `DatabaseLock`, `InitialisationMarker`,
`RunwayDirectory`), come quello di Events (`DatabaseErrors`). Un pacchetto che il codice usa per nome va referenziato direttamente:
qui si fa ora, e il riferimento fissa insieme la versione corretta.

Il repository non scrive nessun `SslMode` (le stringhe di connessione stanno nei segreti di ogni installazione, che da qui non si
leggono): se un'installazione usa `VerifyCA`/`VerifyFull` senza CA non si sa. L'audit comunque non lo sa, e non deve: il pacchetto si
corregge, non si zittisce.

## 3. Le strade, e la scelta

1. **Il pinning transitivo** (`CentralPackageTransitivePinningEnabled`): fissa nei grafi transitivi ogni versione del file, non solo
   questa. Più largo di ciò che serve, come per il Pkcs.
2. **Un riferimento diretto nel nucleo**, con la `PackageVersion` che c'è già. **Scelta**: tocca solo questo pacchetto, e dice il vero
   (il nucleo lo usa).
3. Zittire l'audit (`NoWarn`, `NuGetAudit=false`): escluso, come per il Pkcs.

**Pomelo 9.0.0 accetta 2.6.x**: la sua dipendenza è un minimo (`≥ 2.4.0`), senza tetto. Verificato in questa PR con la suite
d'integrazione intera, che applica la catena delle migrazioni di ogni contesto su MariaDB 11.4.10 e fa passare ogni query dal driver
nuovo (il risultato è nella PR).

## 4. Quando se ne va

Il riferimento resta: il nucleo nomina il pacchetto. Quello che cambia nel tempo è la versione: quando Pomelo chiederà da sé una
versione ≥ 2.6.1, la `PackageVersion` potrà seguirla; non deve mai scendere sotto **2.6.1**. Le installazioni già consegnate (0.14.0 e
prima) portano 2.4.0: le chiude la consegna successiva.

## Da portare nel piano

- `00-piano-di-progettazione.md`, la tabella delle dipendenze (riga di MySqlConnector, «≥ 2.4 · dipendenza Pomelo»): **2.6.2,
  riferito dal nucleo**, per l'avviso del 28 ottobre 2026; mai sotto 2.6.1. Versione e changelog (0.14.1).
- `01-design-m0.md` §0.3 (le versioni fissate), stessa riga.

[g]: https://github.com/advisories/GHSA-473q-m89c-ghf8
