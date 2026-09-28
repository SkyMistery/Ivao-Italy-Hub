# La consegna del pacchetto: dal rilascio del tag, non da un publish locale

**Data:** 27 settembre 2026
**Stato:** **decisa da Carmine in chat, 27 settembre 2026** (il §2). Non c'è un commento su GitHub da collegare: la
decisione è della chat con il master. Il modo del §3 è della sessione di lavoro che ha scritto lo script, e lo legge la revisione.
**Regola applicata:** piano §11.3 («Deploy su Plesk — il modello di vIPI, riusato»), che cambia in due punti. Nessun codice del
prodotto è toccato: uno script in `tools/`, un runbook in `docs/`, questa nota.

## 1. Il problema

- Il piano §11.3 punto 1 dice che il pacchetto è un `dotnet publish -c Release -r linux-x64 --self-contained`, cioè un publish
  **fatto su questa macchina**. Due difetti, tutti e due già pagati da vIPI:
  - **il timbro**: il commit che il pacchetto porta (`/api/version`, `diagnostics/startup.txt`) è quello della cartella di lavoro
    di chi pubblica, con o senza modifiche non committate. Un pacchetto che dice un commit e contiene altro è peggio di uno che non
    dice niente;
  - **i segreti**: nella cartella di un publish locale può finire qualunque cosa. Il 24 e il 31 agosto 2026 il file dei segreti di
    produzione di vIPI (la connection string con la password, il `ClientSecret` di IVAO) era nella cartella da caricare, e lo zip
    costruito camminando la cartella l'ha spedito per posta.
- `.github/workflows/release.yml` esiste da M0: a ogni tag `v*` fa girare tutta la CI, pubblica **su un checkout pulito** e allega
  lo zip al rilascio GitHub. Il pacchetto giusto c'è già; mancava il modo di consegnarlo.

## 2. Che cosa ha deciso Carmine (in chat, 27 settembre 2026)

1. L'hub si consegna **come vIPI, con una differenza**: il **pacchetto completo viene dal rilascio GitHub del tag**, costruito da
   `release.yml` su un checkout pulito. Il suo timbro è esatto, e nessun file di questa macchina, segreti compresi, può esserci
   dentro. Non si fa più un `dotnet publish` locale per consegnare.
2. Il master prepara la consegna in locale in **`artifacts/publish/`** (fuori da git): **solo la consegna corrente**.
3. La consegna di prima passa in **`artifacts/publish_old/<versione>/`**, una cartella per consegna, con i **fogli di allora**.
4. Il nome è la **versione** (`0.2.0`), non la data con una lettera come in vIPI.

Restano le regole di vIPI: lo zip si costruisce dall'**elenco dichiarato** con le impronte, mai camminando la cartella; una seconda
rete ferma un file dichiarato che contiene una password o un `ClientSecret`; i documenti non si mescolano ai file da caricare; il
pacchetto incrementale porta solo i file cambiati rispetto al completo di prima, **scelti** da chi consegna e non presi alla cieca
dal confronto; `restart.txt` è un attrezzo per Passenger.

## 3. Come (scelte della sessione di lavoro, per la revisione)

**`tools/prepare-delivery.ps1`**, PowerShell 5.1, codice e commenti in inglese (`CLAUDE.md` §1), quattro azioni; il runbook è
`docs/DELIVERING.md`.

- **Fetch** `-Tag v0.2.0`: `gh release download` dello zip del rilascio in una cartella temporanea; controlla le voci (nessun percorso
  assoluto, nessun `..`, niente di ciò che è dell'installazione); legge il **timbro** dalla risorsa di versione di `IvaoHub.Web.dll`
  (`<versione>+<commit>`, la stessa stringa di `/api/version`) e lo confronta con il commit a cui punta il tag **su origin**
  (`git ls-remote`); solo se coincidono ruota la consegna corrente in `publish_old/<sua versione>/` e scompatta in
  `publish/full-<versione>/`, con un `RELEASE.txt` che elenca ogni file del rilascio con la sua impronta. Si ferma se in `publish/`
  ci sono due `full-*` (la trappola di vIPI del 6 settembre), se la cartella di destinazione in `publish_old/` esiste già, o se la
  versione nuova è più vecchia della corrente.
- **Diff**: confronta per impronta `RELEASE.txt` del pacchetto nuovo con quello del completo precedente in `publish_old/` e scrive
  `publish/candidates-<versione>.txt`, **da correggere a mano**, in quattro gruppi con una riga di spiegazione ciascuno; elenca
  i file tolti dal rilascio (restano sul server, da scrivere nel foglio) e, se git ha i due commit, le migrazioni fra i due.
- **Manifest** `-List <file>`: ogni riga deve essere un file del rilascio, **identico** a quello scaricato (impronta di
  `RELEASE.txt`); passa per le reti; copia i file in `publish/only-<N>-files-<versione>/` e scrive `MANIFEST.txt`. Con `-Full` il
  pacchetto è `full-<versione>/` stesso.
- **Zip** `-Sheets <file o cartelle>`: dal solo `MANIFEST.txt` + i fogli + `restart.txt`, in tre rami (`<pacchetto>/`, `docs/`,
  `restart.txt`); voci scritte a mano con le barre giuste (mai `Compress-Archive`, che in 5.1 scrive le barre rovesciate); `.sha256`
  accanto.
- **`-WhatIf`** su ogni azione: dice che cosa farebbe e non scrive niente.

Le scelte che la revisione deve guardare:

1. **Nomi in inglese**: `MANIFEST.txt` invece di `IMPRONTE.txt`, `only-<N>-files-<versione>` invece di `solo-N-file-<versione>`,
   `delivery-<pacchetto>.zip`. `CLAUDE.md` §1 vuole in inglese i nomi dei file, e il piano 0.20 ha tolto le parole italiane dalle
   cartelle di runtime perché chi forka non ne trovi. Il `full-<versione>` è il nome della decisione stessa.
2. **La rete dei segreti guarda una chiave con un valore**, non il solo nome della chiave come in vIPI. Il pacchetto porta apposta
   `config/*.example.json` (README «Packaging») con `"ClientSecret": "<client secret>"`, e i fogli della #156 portano un modello del
   file dei segreti con `SCRIVI-QUI-…`: un valore vuoto, un `<segnaposto>` o PAROLE-MAIUSCOLE-CON-TRATTINI (senza cifre, quindi non
   un GUID) è un modulo da compilare; ogni altro valore sotto una chiave che finisce per `password`, `pwd`, `secret`, `apikey`,
   `token`, o dopo un `Password=`/`Pwd=`, ferma la consegna. In più: una chiave privata, una chiave di Data Protection. Guarda solo
   i file di testo (la lezione di vIPI sull'assembly che nomina `ClientSecret`), e guarda anche i **fogli**.
3. **I percorsi dell'installazione** (`secrets/`, `hub-keys/`, `media/`, `logs/`, `diagnostics/`, `tmp/`, `config/division.json`,
   `config/ivao-oauth.json`, `*.env`, `*.pem`, `*.key`, `*.pfx`) fermano la consegna in qualunque elenco, qualunque cosa contengano.
4. **Gli assembly dell'hub viaggiano tutti insieme**, e Manifest lo impone. Ogni rilascio alza la versione, `AssemblyVersion` ne
   discende, e un `IvaoHub.Web.dll` nuovo accanto a un `IvaoHub.Core.dll` della versione prima non si carica. È la differenza più
   grande da vIPI, dove si spedivano i soli progetti cambiati.
5. **`appsettings.Development.json` non si consegna mai**: sta nel rilascio (misurato, §4) con la password del database di
   sviluppo, la produzione non lo legge. Diff lo scrive già commentato, e la rete lo ferma se qualcuno lo rimette.
6. **I fogli sono un parametro, senza un percorso predefinito** (indicazione del master): il foglio italiano per Ivao.It lo scrive la
   sessione dell'installazione di prova in `docs/internal/deploy/` (PR #156), e il runbook lo cita come esempio.
7. **Niente guida italiana in più** sotto `docs/internal/`: il runbook è `docs/DELIVERING.md`, il foglio per chi carica è della #156.
   Una terza copia degli stessi passi invecchierebbe da sola.

## 4. Che cosa è stato misurato

- Il rilascio che esiste, **`v0.1.0-m0`** (scaricato in sola lettura, 27 settembre 2026): 452 file, fra cui
  `appsettings.Development.json` (con la connection string di sviluppo), `IvaoHub.Web.staticwebassets.endpoints.json`, l'apphost
  `IvaoHub.Web` e `createdump`, i `.pdb` e gli `.xml` degli assembly dell'hub. Il suo timbro dice `0.1.0+fc0edb2…`, il tag dice
  `0.1.0-m0`: Fetch lo **rifiuta**, giustamente. Ripreso come se fosse `v0.1.0`, il giro completo passa: 451 file consegnati, la
  rete non suona su nessun file vero né sul modello dei segreti della #156, zip di 52 MB con le voci giuste.
- Le prove negative, su un rilascio finto con due `IvaoHub.Web.dll` veri timbrati: commit sbagliato, versione sbagliata, file del
  rilascio cambiato dopo il download, file dichiarato cambiato dopo il manifest, file dei segreti lasciato nella cartella da
  caricare, foglio compilato con una password vera, un solo assembly dell'hub su due, un percorso dell'installazione nell'elenco,
  due `full-*`, rotazione su una cartella che esiste, tag inesistente su origin. Si fermano tutte, e nessuna sposta niente.

## 5. Limiti

- Il timbro si confronta con `v<Version>`: vale da quando il tag è `v` più la `<Version>` di `Directory.Build.props`, la regola che
  porta la nota della versione del sito (in lavorazione, 27 settembre 2026). Con il `Version` fermo a `0.1.0` ogni tag nuovo
  verrebbe rifiutato, e va bene così: un pacchetto che dice un numero diverso dal suo tag non si consegna.
- Il **bit di esecuzione** dell'apphost si perde scompattando su Windows e caricando via FTP: lo rimette chi carica (§11.3 punto 6).
  Lo script non lo può fare.
- Lo script non tocca il server e non sa che cosa ci sia sopra: il confronto è sempre con **il completo della consegna di prima**. Se
  una consegna è stata saltata o caricata a metà, il confronto va fatto con `-Against <versione>` a mano.
- **Proposta, non decisa**: togliere `appsettings.Development.json` dal publish (`CopyToPublishDirectory="Never"` nel `.csproj`
  del Web). È una modifica del nucleo, con la sua PR; finché non c'è, lo tiene fuori la lista.

## Da portare nel piano

- **§11.3 punto 1** (Pacchetto): il pacchetto completo è lo **zip del rilascio GitHub del tag** `v<versione>`, costruito da
  `release.yml` su un checkout pulito dopo tutta la CI; non si consegna un publish locale. Il master lo scarica e prepara la
  consegna con `tools/prepare-delivery.ps1` (Fetch → Diff → Manifest → Zip, runbook `docs/DELIVERING.md`) in `artifacts/publish/`,
  solo la consegna corrente; quella di prima va in `artifacts/publish_old/<versione>/` con i suoi fogli. Il timbro del pacchetto
  deve essere `<versione>+<commit del tag>`, o non si consegna. Lo zip di consegna si costruisce da `MANIFEST.txt` (impronte),
  mai dalla cartella, con la rete dei segreti; i fogli per chi carica stanno in `docs/internal/deploy/`.
- **§11.3 punto 6** (Aggiornamento): si carica il ramo `full-<versione>/` o `only-<N>-files-<versione>/` dello zip di consegna,
  controllando le impronte di `MANIFEST.txt`; gli assembly `IvaoHub.*` e `IvaoHub.Web.deps.json` **sempre tutti insieme**;
  `appsettings.Development.json` mai; i file tolti dal rilascio restano sul server e il foglio li nomina; `restart.txt` in `tmp/`
  per ultimo.
