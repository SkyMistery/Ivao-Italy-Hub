# L'immagine della CI fissata a Ubuntu 24.04

- **Data**: 9 novembre 2026
- **Stato**: decisa da Carmine in chat («Fissa ubuntu-24.04», 9 nov 2026), fra le due strade proposte dal master
- **Classificazione** (`CLAUDE.md` §5): **(c)**, configurazione della CI (`.github/`, file del maintainer)

## Che cosa è successo

Nella notte fra l'8 e il 9 novembre `ubuntu-latest` è passato da `ubuntu-24.04` (20261101.360.1, ultimo `main` verde,
run 42460560660) a `ubuntu-26.04` (20261101.186.1). Sulla nuova immagine il limite degli `inotify` per utente è 128, e
gli host dei test d'integrazione lo esauriscono: ogni `WebApplicationFactory` apre un `FileSystemWatcher` per i file
di configurazione con `reloadOnChange: true` (`Program.cs`, segreti e OAuth). Risultato sulla #477, che non tocca .NET:
711 test d'integrazione su 2410 rossi nell'`InitializeAsync`, `IOException: The configured user limit (128) on the
number of inotify instances has been reached`, uguali al rilancio. Avrebbe colpito ogni PR e `main`.

## La scelta

`runs-on: ubuntu-24.04` in tutti e quattro i job (`build-test`, `core-guard`, `release` due volte): la CI torna
com'era l'8 novembre, e la build di un rilascio gira sulla stessa immagine dei test.

## Le alternative

- **Restare su 26.04 e alzare il limite** (`sudo sysctl fs.inotify.max_user_instances=…` prima dei test) oppure
  `DOTNET_USE_POLLING_FILE_WATCHER=true` nel job: lascia la CI su un'immagine appena uscita, mai provata col resto
  della batteria (MariaDB, Playwright, il pacchetto linux-x64).
- **Spegnere `reloadOnChange` negli host di prova**: tocca come i test costruiscono l'host; da valutare quando si
  passerà a 26.04, non sotto un rosso.

Il passaggio a un'immagine nuova sarà un cambio a parte, con la sua PR e la batteria intera.

## Da portare nel piano

- §11.2 (CI): la CI gira su un'immagine fissata (`ubuntu-24.04`), non su `ubuntu-latest`; cambiarla è una
  decisione a parte.
