# Il Pkcs corretto (E10q)

**Data:** 15 ottobre 2026 — fase E10q di M4 (nucleo), PR #265
**Stato:** **decisa** (Carmine, 14 ottobre 2026, data in chat alla sessione master e pubblicata dal master su sua istruzione
nell'[issue #264][i264]): un piccolo pull request del nucleo, diretto su `main` e non in coda, che fissa il pacchetto a 10.0.13 con la
gestione centrale dei pacchetti, **senza** zittire l'audit, e alza la versione di PATCH. L'issue è il sì del maintainer a toccare
`Directory.Packages.props` e `Directory.Build.props` per questo.
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**, **Correzione**: la gestione centrale delle versioni (`Directory.Packages.props`) e
l'audit di NuGet con gli avvisi come errori esistono già; si usano come sono. Nessun meccanismo nuovo.

## 1. Che cosa

- `Directory.Packages.props`: una `PackageVersion` di `System.Security.Cryptography.Pkcs` a **10.0.13**, in un gruppo suo («Security
  fixes of transitive packages»).
- `src/IvaoHub.Core/IvaoHub.Core.csproj`: una `PackageReference` diretta al pacchetto, con un commento che dice perché c'è e quando se
  ne va. Il riferimento sale a ogni progetto sopra il nucleo (Web, i tre moduli, i due progetti di test), quindi tutti risolvono 10.0.13.
- `Directory.Build.props`: `<Version>` da 0.6.7 a **0.6.8** — PATCH per la regola scritta accanto: solo una correzione, nessuna
  migrazione, nessuna pagina o sezione nuova.

## 2. Perché

Dal 13 ottobre 2026 due avvisi su `System.Security.Cryptography.Pkcs` 10.0.0 fanno fallire `dotnet restore` (NU1902, NU1903, avvisi
trattati come errori) su ogni progetto, e con lui `build-test`:

- [GHSA-846f-f764-76mx][g1] — **alta**, CVE-2026-84008: `SignedCms.CheckSignature` può rileggere più volte dati X.509 scelti da chi
  attacca, un consumo di CPU senza autenticazione (DoS); Windows, Linux, macOS ([dotnet/announcements#468][ann]);
- [GHSA-2fmv-rv2c-59x7][g2] — **moderata**: `EnvelopedCms.Decode` fa un lavoro quadratico su attributi CMS/PKCS#7; solo Windows.

Per tutti e due le versioni 10.0.0–10.0.12 sono colpite, la prima corretta è **10.0.13**. Il pacchetto non è nostro: lo porta MailKit
attraverso MimeKit (`MailKit 4.17.0 → MimeKit 4.17.0 → System.Security.Cryptography.Pkcs 10.0.0`), e solo nel nucleo. L'hub non usa
`SignedCms` né `EnvelopedCms` (le mail partono senza S/MIME), ma l'audit non lo sa, e non deve: il pacchetto si corregge, non si
zittisce.

## 3. Le due strade, e la scelta

1. **Il pinning transitivo** (`CentralPackageTransitivePinningEnabled`) con la sola `PackageVersion`: una riga in meno, ma fissa nei grafi
   transitivi **ogni** versione del file (EF Core, `Microsoft.AspNetCore.*`, …), non solo questa; un cambio più largo di ciò che serve.
2. **Una `PackageVersion` e un riferimento diretto nell'unico progetto che lo porta**, il nucleo: tocca solo questo pacchetto. **Scelta.**

Zittire l'audit (`NoWarn` di NU1902/NU1903, `NuGetAudit=false`) è escluso dall'issue.

Il riferimento diretto se ne va quando una versione di MimeKit dipenderà da sé da un Pkcs corretto (lo dirà `dotnet nuget why`).

## 4. Il runtime del pacchetto self-contained

Il CVE è di .NET, e l'avviso chiede anche il runtime 10.0.13 (o l'SDK 10.0.402). Quello che si è misurato:

- **Il `Pkcs.dll` pubblicato è quello corretto anche con un runtime vecchio.** `System.Security.Cryptography.Pkcs` sta anche nel
  framework condiviso `Microsoft.AspNetCore.App`; in una publish self-contained il pacchetto (versione di file 10.0.13) vince sul runtime
  pack. Provato in locale con l'SDK 10.0.401 (runtime 10.0.12): `dotnet publish src/IvaoHub.Web -c Release -r linux-x64 --self-contained`
  mette `System.Security.Cryptography.Pkcs.dll` 10.0.1326 accanto a `System.Private.CoreLib.dll` 10.0.1226.
- **Il pacchetto si costruisce solo in `release.yml`**, su GitHub (`tools/prepare-delivery.ps1` scarica lo zip della release). Lì
  `actions/setup-dotnet@v6` legge `global.json` (`10.0.100`, `rollForward: latestFeature`) e installa **l'ultimo SDK 10.0**: l'ultimo
  `build-test` di `main` (14 ottobre, `c232448`) ha installato l'SDK **10.0.402** con il runtime **10.0.13**, quelli corretti. Una
  release fatta oggi include quindi il runtime corretto.
- **Non serve cambiare niente in `.github/` né in `global.json`** per questa correzione. Se il maintainer vuole che nessun SDK più vecchio
  possa più costruire l'hub (in locale `rollForward` accetta anche un SDK 10.0.1xx), il passo è alzare `sdk.version` in `global.json` a
  `10.0.402`: è una scelta sua, e non è in questa PR.
- **Le installazioni già consegnate** (0.6.7 e prima) portano Pkcs 10.0.0: le chiude la consegna di 0.6.8.

## Da portare nel piano

- `00-piano-di-progettazione.md` §11.3 (il pacchetto) o la sezione delle dipendenze: un pacchetto transitivo con un avviso si corregge
  con una `PackageVersion` e un riferimento diretto nel progetto che lo porta, mai zittendo l'audit; il runtime incluso nel pacchetto
  segue l'ultimo SDK 10.0 che `setup-dotnet` installa. Versione e changelog (0.6.8).
- `01-design-m0.md` §0.3 (le versioni fissate): `System.Security.Cryptography.Pkcs` 10.0.13, per gli avvisi del 13 ottobre 2026.
- `10-piano-implementazione-m4.md`: E10q, scritta in questa PR.

[i264]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/264
[g1]: https://github.com/advisories/GHSA-846f-f764-76mx
[g2]: https://github.com/advisories/GHSA-2fmv-rv2c-59x7
[ann]: https://github.com/dotnet/announcements/issues/468
