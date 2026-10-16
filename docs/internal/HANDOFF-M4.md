# HANDOFF — stato di M4 (Events)

> Documento **interno** (italiano). È il punto d'ingresso di ogni sessione che lavora al modulo Events. Lo scrive **la sessione
> di lavoro** di ogni fase, alla fine, con un paragrafo «Che cosa ha lasciato <fase>» in cima alla sezione «Lo stato». Lo stato
> generale del progetto sta in `HANDOFF.md`, che scrive solo il master. Le regole — chi unisce, che cosa non si tocca, come si
> ottiene una decisione — sono in `CLAUDE.md` §0 e in `10-piano-implementazione-m4.md`, «Regole di tutte le fasi», e non si
> ripetono qui.

**Ultimo aggiornamento:** 16 ottobre 2026 — **fase E13a** (modulo, la quarta di **M4b**: le verifiche dopo un evento e le statistiche), sul
branch `m4/e13a-after-checks`, **PR #278** verso `main`, **in coda dopo la #271** di E17 **e la #277** di E10t (del nucleo): nata
`--no-track` dalla testa di E17 `2aebf704`, ha unito la testa di E10t `712bce8` (con `main` a `4ad747a`) e la testa nuova di E17 `92a7ff7`
(con E12 `1887c27` ed E10s `00b0609`), e dopo la revisione le teste di E10t `e17dd8c` (con `main` a `b5d3fb9`, E12 unita) e `2255412`
(con `main` a `2938ae8`, E10s unita) e di E17 `02ea4aa` (con E10s `9e40aff`); dopo la seconda lettura, approvabile, `main` a `85d15a7`
(E17 unita) e la testa di E10t `ee9772c`. **La #277 entra prima, poi questa.** La nota `2026-10-16-le-verifiche-dopo-l-evento` è **decisa** da Carmine sull'issue #272: (A) e (A)
alle scelte 5 e 7 ([la prima risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/272#issuecomment-6219761876)), e dopo la
lettura della PR **sì alle dieci letture** con due condizioni, fatte sulla stessa PR — il conto per scalo e «non contato» distinto da 0
([la seconda](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/272#issuecomment-6222349300)). Da E13a, sei ore dopo la fine di un evento, `events-after` trova ogni ora, a lotti, le prenotazioni volate, i turni fatti o un
no-show da proporre con chi ha coperto la postazione, conta chi ha volato senza prenotare, e scrive le statistiche, che lo staff legge e
ricalcola nella scheda «Statistiche» («Che cosa ha lasciato E13a», sotto).

**L'aggiornamento di E17:** 17 ottobre 2026 — **fase E17** (modulo, la seconda e ultima di **M4c**: le attività parallele di un evento in
presenza, i posti sotto un blocco, «Duplica» per domande e attività, la conservazione breve con `events-retention`, il giro completo di
M4c), sul branch `m4/e17-in-person-activities`, **PR #271** verso `main`, **in coda dopo la #269** di E10s (la #268 di E12 è unita), approvabile alla [seconda lettura del
revisore](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/271#issuecomment-6222329978): nata
`--no-track` da `main` a `c232448`, ha unito la testa di E12 `a44b70f` (con E11b) e ha **rifatto la sua migrazione sopra quella di E12** (la
fila ATC migra lo stesso contesto), poi le teste di E10s; alla fine le teste nuove di E12 `1887c27` (le correzioni delle due letture del revisore e le due
condizioni di Carmine) e di E10s `00b0609`, tutte e due con `main` a `4ad747a` (E11b, E4d, il piano 1.34); infine la testa di E10s `9e40aff`, con `main` a `b5d3fb9` (E12 unita, la
#268, e la #273 dei tour). La nota
`2026-10-15-le-attivita-e-la-conservazione-breve` è **decisa** da Carmine sull'[issue
#267](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267#issuecomment-6215521232): (A), (A) e (B) — prima la fase del nucleo E10s, ed
E17 in coda dopo di lei. Con E17 **M4c è finita nel codice**; il rapporto di chiusura di M4 aspetta M4b («Che cosa ha lasciato E17», sotto).

**L'aggiornamento di E12:** 16 ottobre 2026 — **fase E12** (modulo, la terza di **M4b**: la pubblicazione del roster, le mail dei turni,
la cessione di un turno, `events.atcCoverage`), sul branch `m4/e12-roster-publication`, **PR #268** verso `main`, in fondo alla coda #263 → #262 → #268 (E4d ed E11b ora unite): nata `--no-track` dalla testa di E11b `973f385`, ha unito
le sue teste fino a `33ed36f` (con E4d `279c8cd` e `main` a `3b8d7d79`) e poi **`main` a `4ad747ad`** (il piano 1.34, la `0.7.0`).
La nota `2026-10-14-la-pubblicazione-del-roster-e-la-cessione` è **decisa** da Carmine sulla #268 ([la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/268#issuecomment-6216437241): sì alle dieci letture,
con due condizioni, ora nel codice), e i rilievi del revisore sono corretti sulla stessa PR. Da E12, a inizio − `rosterPublishDays`, `events-roster` **pubblica il roster una volta** e dice a ogni
controllore i suoi turni in una mail; dopo, ogni correzione avvisa subito chi sapeva del turno; c'è la pagina `/events/{slug}/roster` (i nomi
solo a chi ha fatto il login), i turni in `/events/mine` e in `events.myEvents`, la **cessione** di un turno decisa dallo staff, e il blocco
**`events.atcCoverage`** («Che cosa ha lasciato E12», sotto).

**L'aggiornamento di E11b:** 16 ottobre 2026 — **fase E11b** (modulo, la seconda di **M4b**: la proposta del roster ATC e la correzione dello
staff), sul branch `m4/e11b-roster-proposal`, **PR #262** verso `main`, **in coda dopo la #263** (E4d, la coda #263 → #262 → #268): nata
`--no-track` dalla testa di E11a `c967bc1`, ne ha unito la testa dopo la seconda lettura `ac52c4b` (con E16) e `main` a `cea6244`, dopo
l'unione di E16 (#255) ed E11a (#256) **`main` a `c232448`**, poi `main` a `e041bb04` (il Pkcs corretto, #265) e infine **la testa di E4d
`279c8cd`** (con `main` a `3b8d7d79`, E10p #261). La nota `2026-10-13-la-proposta-del-roster` è **decisa** da Carmine sull'[issue #259](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259):
(A) per «`atcData` di IT» (`vipi` quando l'utente di sola lettura della vista esiste sull'host) e le dieci letture, tre con una condizione
ora nel codice; i rilievi del revisore sulla #262 sono corretti sulla stessa PR («Che cosa ha lasciato E11b», sotto). Da E11b, alla
chiusura delle disponibilità di un evento con un roster, `events-roster` **propone il roster una volta**, con il perché di ogni turno, e lo
staff dell'ATC lo **corregge** — aggiunge, sposta, toglie, con gli avvisi dove un turno esce dalle regole —, sotto le postazioni.

**L'aggiornamento di E4d:** 16 ottobre 2026 — **fase E4d** (modulo: gli eventi prendono tre pezzi del nucleo uniti negli ultimi giorni),
sul branch `m4/e4d-events-adopt-core`, **PR #263** verso `main`, **in fondo alla coda #263 → #262 → #268** che il revisore ha chiesto
([il suo commento](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/263#issuecomment-6216628056)): nata da `main` a `c232448` (con E16
#255, E11a #256 e A13f #258), approvabile per il revisore, e il 16 ottobre, su sua richiesta, ha unito `main` a `e041bb04` (E10q, #265, il
Pkcs corretto) perché `build-test` giri, poi `main` a `3b8d7d79` (E10p, #261, le ore in 24). Nessuna migrazione e nessuna decisione nuova; una nota, `2026-10-14-gli-eventi-sul-lettore-dei-nomi`, **decisa** dalla regola
di Carmine sulla #258, perché la PR toglie dai due test del nucleo le righe che le note di E10m ed E10l lasciavano agli eventi. Da E4d gli
eventi leggono i nomi con `MemberNames` (E10m), `events-release` dice il suo fuso, UTC (E10j), i dialog di uno slot e dell'ora privata
chiudono con «Chiudi» (E10l), e le schede «Domande» e «Iscrizioni» di E16, vuote, dicono una frase loro («Che cosa ha lasciato E4d»,
sotto). **E11b (#262)** unisce questo branch, ed E12 (#268) quello di E11b.

**L'aggiornamento di E10p:** 16 ottobre 2026 — **fase E10p** (nucleo: le ore in 24, in un posto solo), sul branch
`m4/e10p-times-24-hour`, **PR #261** verso `main`, **senza coda** (tocca solo il nucleo del browser): nata da `main` a `cea6244`,
**decisa** da Carmine sull'issue #260 ([la seconda via](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/260#issuecomment-6146396089),
e [i tre valori attesi di due test
suoi](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/260#issuecomment-6146818613)), [approvabile senza niente da
correggere](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/261#issuecomment-6164248668), e ha unito `main` a `c232448` (con E16, E11a
e A13f) su richiesta del revisore, poi `main` a `e041bb0` (con E10q, perché `build-test` giri). Da E10p ogni ora del nucleo si scrive in
24 ore anche in inglese, attraverso un solo formattatore («Che cosa ha lasciato E10p», sotto).

**L'aggiornamento di E10q:** 15 ottobre 2026 — **fase E10q** (nucleo: una correzione di sicurezza, l'[issue
#264](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/264)), sul branch `m4/e10q-pkcs-advisory`, **PR #265** verso `main`, **diretta
e non in coda**: nata da `main` a `c232448` (E11a unita). Fissa `System.Security.Cryptography.Pkcs` a **10.0.13** (gli avvisi del 13
ottobre facevano fallire `dotnet restore`, e con lui `build-test` della #261, #262 e #263) e porta la versione a **0.6.8** («Che cosa ha
lasciato E10q», sotto). Dopo la sua unione la #261, la #262 e la #263 uniscono `main`; lo coordina la sessione che coordina.

**L'aggiornamento di E11a:** 12 ottobre 2026 — **fase E11a** (modulo, la prima di **M4b**: le postazioni ATC di un evento e la
disponibilità dei controllori), sul branch `m4/e11a-positions-and-availability`, **PR #256** verso `main`, **in coda dopo la #255** di
E16, l'ultima della coda del master (**#242 → #250 → #252 → #251 → #249 → #239 → #254 → #255 → #256**): nata dal branch di E16 a
`dd5b263`, ha unito la sua testa `78fc9b6`, che porta tutta la coda sotto. La nota `2026-10-12-le-postazioni-e-la-disponibilita` è
**decisa** da Carmine sulla #256 (sì alle dieci letture, fra loro cinque letture del server scritte a mano, e la (B) alla seconda domanda,
le finestre dei candidati); le correzioni delle due letture del revisore sono fatte. Da E11a un evento con
un roster apre le sue **postazioni** — lo staff dei FIR quelle del suo FIR, anche dalla sezione «ATC degli eventi» —, e un membro dà la sua
**disponibilità** dalla pagina dell'evento, con le finestre, fino alla chiusura; lo staff legge chi è disponibile su ogni postazione («Che
cosa ha lasciato E11a», sotto).

**L'aggiornamento di E16:** 12 ottobre 2026 — **fase E16** (modulo, la prima di **M4c**: le domande e l'iscrizione a un evento in
presenza, ed `evt_event_stats` intera), sul branch `m4/e16-in-person-registration`, **PR #255** verso `main`, **in coda dopo la #254** di
E10o, l'ottava della coda del master (**#242 → #250 → #252 → #251 → #249 → #239 → #254 → #255**, con E11a dopo): nata dal branch di E8b a
`4bc847e`, ha unito la testa di E10o `a97a1fd`, che porta tutta la coda sotto. La nota `2026-10-12-le-domande-e-l-iscrizione` è
**decisa** da Carmine sull'issue #253 (A a tutte e due le domande: le etichette del form del membro da E10o; «Togli» un'iscrizione con il
motivo e una mail). Da E16 un evento può essere **in presenza**, con il luogo: lo staff scrive le sue domande, un membro si iscrive
rispondendo — il server controlla ogni risposta per tipo — e cambia o si ritira fino all'inizio, lo staff vede le iscrizioni e le somme
e ne toglie una con un motivo («Che cosa ha lasciato E16», sotto). Prima, **fase E10o** (nucleo: un campo dice il suo nome), sul branch
`m4/e10o-field-names-in-schema`, **PR #254** verso `main`, **in coda dopo la #239**, la settima della coda **#242 → #250 → #252 → #251
→ #249 → #239 → #254**, con E16 dopo: nata da `main` a `a75ed6c`, **decisa** da Carmine sull'issue #253 ([la sua risposta, punto
1](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540): A, prima una fase del nucleo), e ha unito la testa
di E10j `0963908` (con E8b, E10n, E10m, E10l, E4c e `main` ad `a75ed6c`). Da E10o un campo del form generato può dire **il suo nome
nello schema** (`.meta({ label })`), già nella lingua sullo schermo, e la sua select opzionale dice «Nessuna scelta» con una parola del
nucleo: è quello che serve al form del membro di E16, costruito dalle domande di un evento («Che cosa ha lasciato E10o», sotto).

**L'aggiornamento di E10j:** 12 ottobre 2026 — **fase E10j** (nucleo: i job che recuperano e il POST pianificato), sul branch
`m4/e10j-jobs-catch-up`, **PR #239** verso `main`, **in coda dopo la #249**, l'ultima della coda **#242 → #250 → #252 → #251 → #249 →
#239**: nata da `main` a `0f72737`, **decisa** da Carmine sulla #239 con le sue cinque risposte, la domanda 3 compresa (il token anche
nell'indirizzo, accanto all'intestazione, per un pannello che sa solo «Recupera un URL»: [la risposta
5](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/239#issuecomment-6099399044)), rivista due volte — alla seconda lettura [niente
più da correggere](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/239#issuecomment-6113746824) —, e ha unito la testa di E8b
`889ba1a` (con E10n, E10m, E10l, E4c e `main` ad `a75ed6c`, E7 compresa). Da E10j ogni job Quartz dell'hub, del nucleo e dei moduli,
gira una volta per occorrenza qualunque processo sia vivo: i giri persi mentre Passenger tiene l'hub spento si recuperano dopo un
avvio, uno dopo l'altro, e l'operazione pianificata dell'host chiama `/api/jobs/run` con il token dell'installazione («Che cosa ha
lasciato E10j», sotto). ⚠️ **Che cosa offre l'operazione pianificata del pannello resta un punto aperto: lo chiude chi la imposta, alla
consegna** (il maintainer non ha accesso al pannello; nota `2026-10-09-i-job-che-recuperano` §2).

**L'aggiornamento di E8b:** 12 ottobre 2026 — **fase E8b** (modulo: «Duplica», la cancellazione dei dati di una persona, il giro completo
di M4a), sul branch `m4/e8b-duplicate-and-m4a-round`, **PR #249** verso `main`, **in coda dopo la #251**, la quinta della coda **#242 →
#250 → #252 → #251 → #249 → #239**: nata dal branch di E7 a `0d766fe`, rivista due volte — approvabile, e alla seconda lettura
[niente più da correggere](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6119780940) —, con le due note **decise**
da Carmine (sull'issue #248 e sulla #249), e ha unito la testa di E10n `b4ea56e` (con E10m, E10l, E4c e `main` ad `a75ed6c`, E7
compresa). Con E8b **M4a è finita nel codice**, e dopo restano E9, fuori dal repository, e M4b o M4c. Da E8b un evento si **duplica**
su una nuova data, come bozza, e la cancellazione dei dati di una persona tocca anche le sue prenotazioni; E8b segna le cinque colonne
della prenotazione che la sua cancellazione svuota (`[ErasedWith(nameof(BookerVid))]`, la riga che la nota di E10n chiede a un modulo),
così anche l'audit di una prenotazione ritirata prima della cancellazione le perde. ⚠️ **Con E10n ed E8b unite cade la regola di Carmine
sulla #233** («nessuna prenotazione su un'installazione vera finché E8b non è unita»); **resta il log binario**, da provare con la prima
prenotazione sull'installazione di prova alla consegna («Che cosa ha lasciato E8b», sotto).

**L'aggiornamento di E10n:** 12 ottobre 2026 — **fase E10n** (nucleo: i dati di una persona cancellata nell'audit delle righe già tolte,
decisa da Carmine sulla #249), sul branch `m4/e10n-erased-audit-rows`, **PR #251** verso `main`, **in coda dopo la #252**, la quarta
della coda **#242 → #250 → #252 → #251 → #249 → #239**: nata da `main` a `bea8c0e`, corretta dopo la revisione (12 ottobre: le due
correzioni e i tre bassi, [i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/251#issuecomment-6119839771)), e ha unito la
testa di E10m `2509a72` (con E10l, E4c e `main` ad `a75ed6c`, E7 compresa). ⚠️ **E8b (#249) la unisce dopo** e segna le cinque colonne
della prenotazione; **nessuna prenotazione su un'installazione vera prima delle due** («Che cosa ha lasciato E10n», sotto).

**L'aggiornamento di E10m:** 12 ottobre 2026 — **fase E10m** (nucleo: un solo lettore dei nomi), sul branch `m4/e10m-names-reader`,
**PR #252** verso `main`, **in coda dopo la #250**, la terza della coda **#242 → #250 → #252 → #251 → #249 → #239**: nata da `main` a
`bea8c0e` (con E10k, la #238), **decisa da Carmine** sull'issue #247, **approvabile senza niente da correggere** per il revisore
([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/252#issuecomment-6119781289)), e ha unito la testa di E10l `2e4164d`
(con E4c e `main` ad `a75ed6c`, E7 compresa). Il nucleo ha `MemberNames.ReadAsync` (`Core/People/`), i fili dei contatti lo usano, e la
formazione e gli eventi ci passano sopra in due piccole fasi di dalberone dopo l'unione («Che cosa ha lasciato E10m», sotto); i tour in
una sessione di Carmine.

**L'aggiornamento di E10l:** 12 ottobre 2026 — **fase E10l** (nucleo: il pulsante di chiusura delle finestre parla la lingua), sul branch
`m4/e10l-dialog-close-label`, **PR #250** verso `main`, **in coda dopo la #242**, la seconda della coda **#242 → #250 → #252 → #251 →
#249 → #239**: nata da `main` a `bea8c0e` (con E10k, la #238), **decisa da Carmine** sull'issue #224, **approvabile senza niente da
correggere** per il revisore ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/250#issuecomment-6113746994)), e ha unito
la testa di E4c `8683d53` (con `main` ad `a75ed6c`, E7 compresa). Il `Dialog` del nucleo c'è; i due dialog degli eventi — quello di uno
slot e, con E7 unita, quello dell'ora privata — ci passano in una fase loro dopo questa («Che cosa ha lasciato E10l», sotto).

**L'aggiornamento prima:** 12 ottobre 2026 — **fase E4c** (gli eventi dicono il loro titolo e le loro liste vuote), sul branch
`m4/e4c-events-titles-and-empty-lists`, **PR #242** verso `main`, **in fondo alla coda** che il revisore ha chiesto il 12 ottobre: nata dal
branch di E10k, ne ha unito le teste fino alla #238, e ha unito **`main` ad `a75ed6c`** (con E7, la #246). Sono unite E1 (#200), E2 (#209), E2b (#212), E3a (#214), E3b (#221), E4 (#223), **E4b (#226)**, **E5 (#228)**, **E6a (#233)**, **E6b (#240)**,
**E7 (#246)**, E10a (#210), E10b (#208), E10c (#204), E10d (#205), E10e (#206), E10f (#213), **E10g (#230)**, **E10h (#232)**, **E10i
(#237)**, **E10k (#238)** ed E15a (#207), il passaggio dei tour al calcolo del nucleo (#211), la `0.6.0` (#216), i piani 1.29 (#217), 1.30 (#229), 1.31
(#234) e **1.32 (#243)**, la parola degli eventi nella ricerca (#222), le altre correzioni del nucleo fino alla `0.6.7` (#218, #219, #225,
**#241** — il test del meteo con `IClock` — e **#244** — l'indirizzo di ritorno dopo l'accesso) e gli spec che dicono al banco che cosa
rimettono a posto (#227). **E4b** non migra niente; la striscia di chi è online sugli scali dell'evento, il giorno dell'evento, l'ha
montata E6b, dentro la pagina («Che cosa ha lasciato E6b», sotto). **In corso, in una coda sola** (il revisore, 12 ottobre 2026,
[sulla #242](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/242#issuecomment-6120883096), perché ogni unione rimetteva in conflitto
le altre sull'intestazione di questo file): **#242 → #250 → #252 → #251 → #249 → #239**. In fondo **E4c** (questa, la #242: il titolo di
`/events` e di `/events/mine`, la frase vuota delle cinque liste generate degli eventi — gli usi degli eventi di E10k), poi **E10l**
(#250), la #252 e la #251, **E8b** (#249: «Duplica», la cancellazione, il giro completo di M4a) ed **E10j** (#239, del nucleo: il recupero
dei giri dei job). Il maintainer unisce dal fondo, una alla volta.
**Il prossimo passo**: unire la coda dal fondo, a cominciare dalla #242. **E7 è unita** (la #246): il ritiro di uno dei due voli collegati
(Carmine sull'issue #245, la strada A: il legame si scioglie e l'altra prenotazione resta) e la nota `2026-10-11-gli-slot-privati`
(decisa sulla #246) sono decisi («Che cosa ha lasciato E7», sotto). **E10i è unita**: un grant su un evento solo scrive l'evento e le sue righe e ne crea i
figli, mai un evento nuovo («Che cosa ha lasciato E10i», sotto); **E10k è unita**: la lista generata dice le sue pagine e la sua frase
vuota, ogni scheda il suo titolo («Che cosa ha lasciato E10k», sotto). Da E3b un evento si **pubblica**, entra nel
calendario e nella ricerca quando si vede e ne esce alla fine, e tiene i suoi file; da E4 ha la sua pagina `/events/{slug}`, sta in
`/events` e nel blocco `events.eventList`, il FOD ne scrive le rotte, e il suo tipo è uno dei tipi degli eventi («Che cosa ha lasciato E4»,
sotto); da E5 ha i suoi **slot pubblici**, caricati da una tabella con le rotazioni e mostrati sulla sua pagina, e il Gate Manager li legge
con un token personale («Che cosa ha lasciato E5», sotto); da E6a **si prenota**, sul server: un pilota prende uno slot pubblico o tutta una
rotazione e ritira fino all'off block, lo staff toglie una prenotazione con un motivo, e chi ha prenotato sa di un annullamento e di nuovi
orari, e di una correzione del volo che ha prenotato («Che cosa ha lasciato E6a», sotto); da E6b **si prenota dalle pagine**: «Prenota» e
«Prenota tutta la rotazione» nel dialog di uno slot, i filtri, l'apertura con il conto alla rovescia, `/events/mine` con «Ritira», il
blocco `events.myEvents`, la scheda «Prenotazioni» dello staff con «Togli», la conferma prima di «Pubblica» e il promemoria del giorno
prima, `events-reminders` («Che cosa ha lasciato E6b», sotto); da E7 ha i suoi **slot privati**: lo staff li genera dalla capacità degli
scali, un pilota ne prenota uno con il suo volo, e un arrivo con la sua partenza dallo stesso scalo, e l'esportazione li porta con lo
slot collegato, lo stesso gate da dare («Che cosa ha lasciato E7», sotto). **Carmine ha risposto sulla #240**
([1–5](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/240#issuecomment-6083877158),
[6](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/240#issuecomment-6083894346)): **sì a tutto**, e la nota
`2026-10-09-le-pagine-delle-prenotazioni` è **decisa**; ⚠️ **un lettore di nomi per VID nel nucleo entra nella coda del nucleo prima
che se ne scriva un quinto** (`EventsPeople` è il quarto, accettato per ora). **Il revisore ha letto la #240**: le quattro correzioni del
dialog di uno slot che chiedeva sono fatte sulla stessa PR. **Le tre domande di E6a hanno la risposta di
Carmine** ([sulla #233](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6069142670), nota
`2026-10-07-le-prenotazioni-sul-server` ora **decisa**): sì alle prime dieci letture; uno slot prenotato si corregge ancora e il pilota è
avvisato quando cambia il volo (`events.bookingChanged`, fatto sulla #233); l'eraser degli eventi resta a E8b, con la regola qui sotto; il
log binario è un punto aperto. ⚠️ **Prima di aprire le prenotazioni di un evento vero** (Carmine, 8 ottobre 2026, sulla #233):
**nessuna prenotazione su un'installazione vera finché E8b non è unita** (l'eraser degli eventi; l'installazione di prova non è
vincolata); e **il formato del log binario della MariaDB si prova con la prima prenotazione sull'installazione di prova, alla prossima
consegna** — con `STATEMENT` quella prenotazione fallisce subito e la lettura 1 della nota di E6a si riapre. **Punto aperto: lo chiude il
maintainer, alla consegna.** **Il master ha letto la #233**: le quattro correzioni che chiedeva sono fatte sulla stessa PR («Che cosa ha
lasciato E6a»). **Alla domanda della #232 Carmine ha detto sì**
([la risposta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6039778269)): la nota di E10h è decisa, e la sua
testa dopo la revisione (`ec9b3b4`) è unita qui. **Carmine ha risposto
sulla #228** ([le sue risposte][a228]): sì alle otto letture della nota `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, ora
**decisa**, e due punti in più, fatti sulla stessa PR — uno slot cade nella finestra del suo evento (sei ore per parte), e l'esportazione
porta la versione del suo contratto. ⚠️ **Il controllo della versione è del nucleo**: lo porta **E10g** (`ContractVersion`, la #230:
«Che cosa ha lasciato E10g», sotto), perché quello dei tour sta nel loro modulo e una copia negli eventi sarebbe lo stesso pezzo scritto
due volte. **Dopo la prova sul banco (7 ottobre) dalberone ha riaperto la #228** per la pagina degli slot: il tipo principale, partenze e
arrivi per scalo, le rotazioni segnate, il dettaglio di uno slot, e `aircraft_types` nell'esportazione — fatti sulla #228, con una nota
**decisa** da Carmine sulla #228 ([le sue risposte][a228b]: sì ai sette punti, e un ottavo: `Hint` resta un pezzo di questa schermata)
(`2026-10-07-gli-slot-sulla-pagina-dell-evento`); **E6a ha unito quella testa** (`25d23f5`, poi `ecf88b9` e `ba53a97`), e l'esportazione
dà `aircraft_types` accanto a `booked_by` e `aircraft_icao`. ⚠️ **Fra E3b ed E4 nessuna consegna e nessun «Pubblica»
sull'installazione di prova** (Carmine, 6 ottobre 2026, [sulla #221][seq221]): la voce di calendario e la riga di ricerca di un evento
pubblicato puntano a `/events/{slug}`, una pagina che porta solo E4 — **con E4 unita dopo la #221 il vincolo cade** (la pagina c'è).
**Le tre domande di E4 hanno la risposta di Carmine** ([sulla #223][ok223]): sì alle cinque letture del pubblico, E4b come fase del
nucleo, la seconda lettura scritta a mano accettata, i tipi come raccomandato — fatti sulla #223. **La
lettura dei preset** (`GET /api/events/kind-presets`) **resta**: Carmine l'ha accettata sulla #214 come scostamento dal design §7.2 (nota
`2026-10-01-la-lettura-dei-preset-dei-tipi`; «Che cosa ha lasciato E3a», sotto). ⚠️
**`EffectivePermission.FromOutside` è uno solo**, con le due vie che lo danno — un grant a una posizione su un altro dipartimento o al
team di un FIR (E2b), un permesso globale da un grant (E10f) — e una condizione sola nel calcolo (nota di E2b, §3.8; «Che cosa ha
lasciato E2b», sotto). **E11a** ed **E11b** trovano in E10c le postazioni della divisione, il rating preferito e il minimo di una
postazione (l'FRA di IVAO); E10a serve a **E13a**, che aspetta anche E12; **E11b** ed **E13a** trovano in E10b la storia di un
controllore e la presenza in un turno; **E14a** ed **E14b** trovano in E10e la distanza nel nucleo; **E14b** trova in E10d il riepilogo
a chi assegna gli award, e non chiama niente: con E10f il riepilogo arriva anche al coordinatore e all'assistente dell'MD; **E15b** (dopo
E14b) trova in E15a le prenotazioni della rete.

## Per chi prende M4 (`dalberone`)

Scritta dal master il 30 settembre 2026, quando Carmine ha affidato **tutta M4** a `dalberone`, fasi del nucleo comprese (nota
`2026-09-30-m4-al-collaboratore`). È quello che le sessioni del maintainer sanno e che M3 può non aver mostrato; ogni punto è
verificato su `origin/main` quel giorno.

- **Le regole di tutte le fasi** di `10-piano-implementazione-m4.md` valgono per te, con una correzione: dove dicono «una sessione di
  lavoro di Carmine» leggi **la tua sessione, nel tuo worktree**, e l'avviso che «`core-guard` non giudica le PR del proprietario» non
  vale più: le tue PR le giudica. `core-guard.sh` riconosce come tuoi `src/IvaoHub.Modules.Events/`, `web/src/modules/events/`,
  `locales/<lang>/events.json` e i test e le spec che cominciano con `Events`/`events` (`EventsSkeletonTests.cs`,
  `web/e2e/full/events-*.spec.ts`); **`tests/IvaoHub.IntegrationTests/SampleEvents.cs`** e le sue migrazioni `…_AddSampleEvents` sono
  il modulo di prova del nucleo, non gli eventi: toccarli è nucleo. La voce «Chi scrive» di `10` è già corretta (piano 1.25).
- **Le fasi del nucleo** (E1, E10a–E10e, E15a) sono **ognuna una PR a sé con una nota nuova** in `decisions/` (caso (b): quale
  meccanismo si estende e perché il modulo non ne fa a meno), unite **prima** della fase del modulo che le usa. Senza la nota
  `core-guard` va rosso sui file del nucleo. Non si mischiano con codice del modulo.
- **Il perimetro di IVAO** (`CLAUDE.md` §3): il codice che nomina IVAO sta in `src/IvaoHub.Core/Ivao/` e nella metà IVAO di
  `src/IvaoHub.Core/Auth/`. E10a, E10b ed E15a toccano `Core/Ivao/`: si estende **l'unico** `IIvaoApiClient`
  (`IIvaoApiClient.cs`, `IvaoApiClient.cs` con `IMemoryCache`; il resiliente è `AddStandardResilienceHandler()` in
  `IvaoServiceCollectionExtensions.cs`), e **anche `FixtureIvaoApiClient.cs`**, che in sviluppo risponde al posto di IVAO. Il modulo
  non parla mai con IVAO. Le risposte si leggono in un lettore unico per i due client (`IvaoTrackerReader` in `IvaoTracker.cs`: «a
  fixture that parsed itself differently from production would be a fixture that proves nothing»).
- **La richiesta al tracker oggi** è `IvaoSessionQuery` (`IvaoTracker.cs`): **per VID** (`userId`), una finestra, facoltativi partenza
  e arrivo; `PageSize = 50` («what the API was measured to accept») e **al più `MaxSessions = 200`** sessioni, poi si smette di
  sfogliare. E10a (senza VID, per postazione e tipo di connessione) cambia la forma della domanda: la misura del limite di pagina e del
  tetto va rifatta con il token vero, non ereditata.
- **Le fixture** si registrano con `tools/record-ivao-fixtures.mjs` (dalla radice, con `config/ivao-oauth.json`): anonimizza il VID nel
  VID dei test e toglie l'oggetto `user` di IVAO; oggi registra per VID, per elenco, aeroporti e `/users/me`. E10a ed E15a aggiungono
  le loro modalità allo script (è un file del nucleo: `tools/`). **Mai una chiamata vera in un test.** Le tracce restano su IVAO circa
  novanta giorni.
- **La cancellazione** (`tests/IvaoHub.IntegrationTests/ErasureTests.cs`): dalla #187 il test legge da solo i contesti di **ogni**
  modulo abilitato e confronta le colonne `…Vid` e `…By` con la lista `PersonColumnsOfTheHub`. **Da E2** (`evt_events.cancelled_by`)
  ogni fase che crea una colonna di persona scrive la sua riga lì, o il test va rosso; è uno dei due casi in cui un test condiviso si
  tocca (`10`, «Regole di tutte le fasi»), e la PR lo dice.
- **Gli aiuti del nucleo che conosci da M3** valgono uguali: `personName`/`isErased` (`web/src/shared/ui/people.ts`) e la colonna
  `col.person` della lista generata (`web/src/shared/list/columns.ts`) per «persona cancellata»; `IHasAssignee` con `OnlyForAssignee`
  (e `AlsoOnCreation`/`AlsoOnDeletion`) per le righe affidate a chi scrive; i grant `firTeam` al team di un FIR; `Refusals` con
  `CrudProblems.Validation` (`src/IvaoHub.Core/Data/Crud/Refusals.cs`) per i rifiuti raccolti a mano. Mai una copia nel modulo.
- **Il banco e2e** (`web/scripts/e2e-server.mjs`): `999001` il web master (`IT-WM`), `999002` il pilota (`?as=pilot`), `999003` l'assistente
  dei tour (`?as=assistant`, `IT-FOAC`), `999004` il trainer (`?as=trainer`, `IT-T01`). **E1 aggiunge il personaggio dell'ED**
  (`?as=events`, `IT-EC`, un VID dopo `999004`). Il database del banco `ivaohub_e2e` **si accumula** fra un giro locale e l'altro: una
  spec si riprende le sue righe (`CONTRIBUTING.md`, «Tests»).
- **La versione**: le tue PR **non alzano** `<Version>` in `Directory.Build.props`; la alza il master prima di un rilascio, con la regola
  scritta lì (la prossima è la `0.5.0`, piano 1.25). Sulla prova (`test.it.ivao.aero`) gira la **`0.4.1`**; tag, rilasci e consegne sono
  del maintainer.
- **Le decisioni**: una domanda a Carmine è un **commento sulla PR** con la nota «Proposta» e la raccomandazione; Carmine risponde lì,
  oppure in chat al master, che pubblica la risposta sulla PR su sua istruzione: il link al commento va nella nota. Il master non
  risponde mai al posto di Carmine.

## Da leggere, nell'ordine

1. `CLAUDE.md` (tutto, §0 per primo) e `CONTRIBUTING.md`.
2. Il piano `00-piano-di-progettazione.md`: l'intestazione e il changelog 1.24, **§9** (la riga Events di §9.2), **§9.3**, **§9.5**
   (il calendario unico e i tipi degli eventi), **§9.7** (contratti nucleo↔moduli, «Privacy dei membri», le collaborazioni
   ATC↔Events e FlightOps↔Events), **§10** (l'API di IVAO), **§13** (la riga M4), **§15** punto 12, **§16**.
3. **Il design `09-design-m4.md`**, tutto: è deciso (§17, con i link ai commenti di Carmine sulla #180).
4. **Le dieci note della fase E0** (`decisions/2026-09-29-*`, elencate in `10`, E0) e le note che decidono come stanno i moduli:
   `2026-09-13-moduli-non-subordinati-ai-dipartimenti`, `2026-09-13-ordine-dei-moduli`,
   `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`, `2026-09-27-i-capi-fir-sul-loro-fir`,
   `2026-09-28-i-job-quando-passenger-spegne-l-hub`.
5. **`10-piano-implementazione-m4.md`**: le regole di tutte le fasi, «Com'è andata» di E0 (che cosa c'è nel codice e che cosa no) e la
   fase che si apre.
6. **I moduli che esistono già**, da leggere e non da toccare né importare: `src/IvaoHub.Modules.FlightOps/` (lo stato dalle date,
   `TourReleaseJob`, le leg come righe figlie con `BeforeAuthorize`, il token dell'agente, `myTours`) e
   `src/IvaoHub.Modules.Training/` (lo scheletro di A4, `TrainingArchitectureTests`).

## Che cosa il nucleo dà già a Events

Verificato nel codice il 29 settembre 2026 (`10`, E0, «Trovato leggendo il codice»):

- **Il modulo** (`IModule`): permessi nel catalogo, impostazioni (`ModuleSettings`, lette campo per campo sopra i predefiniti), tipi
  di notifica, preferenze del membro, audience dei token personali, segmenti riservati, il contesto con la sua storia delle
  migrazioni. `division.json → modules.events.baseDepartment` è già `ED`.
- **Chi lavora**: i grant a una posizione con uno `scope` (`PositionGrantSeed`), al team di un FIR (`firTeam`, A11a), su una riga
  (`resource_scope`, `events:event:{id}`); `IHasStakeholder` con `DeniedToStakeholder`; `ISubmittedByMembers`; `IHasFir`.
- **Calendario, ricerca, award, usi dei file** con `IProjectable`; il job `media-expiry` che elimina un file quando tutti i suoi usi
  sono scaduti; `ProjectionRefresh` per riproiettare (come `TourReleaseJob`).
- **Lista e form generati** (`MapCrud`, `DataList`, `SchemaForm`), `BeforeAuthorize`, `DeletePolicy`, `Refusals` e
  `CrudProblems.Validation`, `BlockDocument` per la descrizione, `MediaPicker`, `CalendarView`.
- **Da IVAO**: `SearchSessionsAsync` **per VID**, `GetNetworkStatusAsync` (senza VID), `IAirportDirectory` con le coordinate,
  `IAircraftTypeDirectory`, `IvaoAirspace.Covers`/`Serves` per «della divisione».
- **La cancellazione dei dati di una persona**: lo pseudonimo nelle colonne `…Vid` e `…By` di ogni contesto dei moduli, e
  `IPersonalDataEraser` per quello che il modulo deve cancellare.

**Che cosa manca, e quale fase lo porta**: ~~i tipi `rfe`, `rfo`, `mse`, `onlineDay` e uno staff degli eventi sul banco (E1)~~
**portati da E1** (la chiave è `online-day`: sotto, «Che cosa ha lasciato E1»); le sessioni
senza VID, con il tipo di connessione (E10a); ~~il VID nelle sessioni condivise (E10b)~~ **portato da E10b** (e la storia di un
controllore: sotto, «Che cosa ha lasciato E10b»); ~~il rating preferito e minimo, le postazioni della
divisione per nominativo (E10c)~~ **portati da E10c** (il minimo è l'FRA di IVAO: sotto, «Che cosa ha lasciato E10c»); ~~la mail a chi assegna (E10d)~~ **portata da E10d** (un riepilogo al giorno: sotto, «Che cosa ha
lasciato E10d»); ~~la distanza nel nucleo (E10e)~~ **portata da E10e** (`GreatCircle` in `Core/Airspace/`: sotto, «Che cosa ha
lasciato E10e»); ~~le prenotazioni ATC della rete (E15a)~~ **portate da E15a** (`IAtcBookingSource`: sotto, «Che cosa ha lasciato
E15a»); la versione del contratto di un programma esterno, che E0 non prevedeva, **portata da E10g** (`ContractVersion`, per
l'esportazione di E5: sotto, «Che cosa ha lasciato E10g»); il membro che cancella la riga che ha mandato, che E0 non prevedeva, **portato
da E10h** (`[WithdrawnByStakeholder]`, per il ritiro della prenotazione di E6a: sotto, «Che cosa ha lasciato E10h»); il grant su un evento
solo che scrive davvero l'evento e le sue righe, che E0 dava per esistente, **portato da E10i** (il guardiano chiede con lo scope della
riga: sotto, «Che cosa ha lasciato E10i»); i job che recuperano i giri persi mentre Passenger tiene l'hub spento e l'indirizzo
dell'operazione pianificata, che E0 lasciava alla coda del maintainer, **portati da E10j** (sotto, «Che cosa ha lasciato E10j»);
l'helper «persona cancellata» e `ErasureTests` che legge ogni modulo sono già arrivati con A12a di M3 (#187): **E8a è tolta** (piano
1.25), e da E2 ogni fase che crea una colonna di persona scrive la sua riga in `ErasureTests`.

## Per i test

VID `761001–761099`, slug `evt-test-` (il master li scrive in `CONTRIBUTING.md` prima che E2 sia unita). **Attenzione all'ED e
all'MD**: i test dei contatti affermano i destinatari esatti dell'MD e seminano `IT-EC` con un indirizzo; nessuno staff dell'ED o
dell'MD con un indirizzo nei test del modulo, i permessi con grant a un VID. Nessuna chiamata a IVAO: fixture.

## Lo stato

*(Qui, in cima, il paragrafo «Che cosa ha lasciato <fase>» di ogni fase chiusa, la più recente per prima.)*

### Che cosa ha lasciato E13a (16 ottobre 2026, branch `m4/e13a-after-checks`, PR #278; nata dalla testa di E17 `2aebf704`, in coda dopo la #271 di E17 e la #277 di E10t, con le loro teste `92a7ff7` e `712bce8` unite)

- **Che cosa c'è** (il dettaglio in `10`, E13a, «Com'è andata»; **una migrazione**, `AddAfterTheEventColumns` (`covered_by_vid` e `flown_unbooked_airports`), l'ultima del contesto dopo
  quella di E17; niente del nucleo — `schema.d.ts` generato e la colonna di persona in `ErasureTests` —; il tracker attraverso
  `INetworkSessionSource` di **E10t**; una nota nuova, `2026-10-16-le-verifiche-dopo-l-evento`, **decisa** da Carmine sulla #272):
  - **Il job** `events-after` (`After/EventsAfterJob.cs`, ogni ora ai 7 minuti UTC), le regole pure (`After/AfterRules.cs`), le statistiche
    (`After/EventStatistics.cs`) e i loro endpoint (`After/StatsEndpoints.cs`: la risorsa `/api/events/stats` e «Ricalcola»,
    `POST /api/events/events/{id}/stats`).
  - **`evt_atc_shifts.covered_by_vid`**: chi ha coperto la postazione accanto a un no-show proposto, dall'archivio.
  - **La lista dei turni del motore** con il nominativo, il controllore e chi ha coperto (`EventAtcShiftRowDto`); **`EventBookingDto`** con
    `flownAt` e `flownCheckedAt`.
  - **La scheda «Statistiche»** (`screens/stats.tsx`) su un evento finito; `attendanceMinimumMinutes` nelle impostazioni.
  - **I test**: integrazione `EventsAfterTests` (10 dopo la revisione; VID **761180–761183**, slug `evt-test-e13a-…`; le persone della sera di LIRF,
    761020–761028, della fixture di E10a); unità `EventsAfterRulesTests` e un caso in `EventsJobTriggersTests`; vitest `stats.test.ts`; il
    giro `full/events-after.spec.ts` (slug `evt-test-e2e-e13a-…`).
- **Che cosa deve sapere la fase dopo**:
  - **E13b** (i no-show): l'esito che il job scrive è `Attended` o `NoShowProposed`, mai `NoShow` né `Excused`, con `AttendanceBy` e
    `AttendanceAt` vuoti; lo staff conferma o giustifica un `NoShowProposed` e segna a mano un turno rimasto `Unknown` (una sorgente che non
    ha risposto per trenta giorni). Le statistiche contano i no-show proposti e confermati insieme: dopo una decisione, «Ricalcola».
  - **E14a/E14b** (i PIREP e gli award): `ReportsSubmitted`, `ReportsAccepted`, `AwardSignals` sono a 0 in `EventStatistics.WriteAsync`; li
    conta la fase che porta le tabelle, e la scheda li mostra allora. Il PIREP automatico del §5.1 si aggiunge come un passo del job, dopo i
    turni e prima della chiusura, con il suo peso nel lotto.
  - **E15b** (la conservazione dei piloti): una prenotazione cancellata porta via `flown_*`; le statistiche di un evento chiuso restano.
  - ⚠️ **Il banco non lancia un job**: la spec prova la scheda con «Ricalcola»; le verifiche si provano con `EventsAfterTests`, che fa
    girare il job a una data del 2001 (`ActivatorUtilities` con un orologio suo).
  - ⚠️ **Chi ha coperto** resta vuoto finché IT è a `atcData: none` (la scelta 5: con il solo tracker non si sa).
  - **Dopo la revisione della #278** (e il sì di Carmine alle dieci letture, con due condizioni): chi ha volato senza prenotare si conta **a
    pezzi, per scalo**, al più quanto sta nel lotto di un giro, e `evt_event_stats.flown_unbooked_airports` dice a quanti scali è arrivato;
    **vuoto vuol dire «non contato»**, mai 0 (`flownUnbooked` vuoto nelle letture). La riga delle statistiche si scrive **sotto il blocco
    della riga dell'evento**: chi la scrive dopo E13a (E14a, E14b, la conservazione di E15b) passa da `EventStatistics.WriteAsync`, dentro una
    transazione, o prende lo stesso blocco.
  - ⚠️ **I VID di prova**: E13a ha usato **761180–761183** della fascia 761180–761198 data da dalberone.

### Che cosa ha lasciato E10t (16 ottobre 2026, branch `m4/e10t-network-sessions`, PR #277, del nucleo; diretta su `main`, nata a `4ad747ad`, con `main` a `b5d3fb9` unito, con E12, poi a `2938ae8`, con E10s, e a `eefb24a`, con E17)

- **Che cosa c'è** (il dettaglio in `10`, E10t, «Com'è andata»; nessuna migrazione, niente nel browser, nessuna riga di un modulo; una
  nota nuova, `2026-10-16-le-sessioni-della-rete-nel-nucleo`, scelta tecnica, caso (b)):
  - **`INetworkSessionSource.SessionsAsync(NetworkSessionQuery, CancellationToken)`** (`src/IvaoHub.Core/Ivao/NetworkSessionSource.cs`),
    scoped, accanto a `IAtcBookingSource`: la ricerca del tracker di E10a con nomi che non dicono IVAO. Le sessioni **iniziate** nella
    finestra (estremi compresi), dalla più recente, una volta per `Id`, al più `Limit`; **`null`** quando la rete non risponde, mai una
    lista vuota al suo posto.
  - **`NetworkSessionQuery(int? Vid, DateTime FromUtc, DateTime ToUtc, string? DepartureIcao = null, string? ArrivalIcao = null,
    NetworkConnection? Connection = null) { Limit }`**: `DefaultLimit` 200 e `MaxLimit` 1000 sono le costanti di `IvaoSessionQuery`;
    un `Limit` fuori da 1…1000 è un'`ArgumentOutOfRangeException` già nell'`init`.
  - **`NetworkConnection { Pilot, Atc }`** e **`NetworkSession(long Id, int Vid, string Callsign, DateTime StartedAt, DateTime
    EndedAt)`**, con `EndedAt` = inizio + durata dichiarata.
  - **I test**: `NetworkSessionSourceTests` (unità, nuova, 9 casi), con un client che registra la domanda e con il client delle
    fixture (la sera di Fiumicino, `tracker-airport-LIRF.json`).
- **Che cosa deve sapere E13a** (che unisce questo branch, poi `main` quando è unita):
  - **sposta le chiamate**: `IIvaoApiClient.SearchSessionsAsync(new IvaoSessionQuery(…))` → `INetworkSessionSource.SessionsAsync(new
    NetworkSessionQuery(…))`, con `Pilot`/`Atc` al posto di `IvaoConnectionType`, e legge `Id`, `Vid`, `Callsign`, `StartedAt`,
    `EndedAt` di ogni sessione; il `using IvaoHub.Core.Ivao;` resta (il test degli eventi lascia passare lo spazio dei nomi);
  - le regole del tracker restano quelle di E10a (nota `il-tracker-senza-vid` §3.6): la finestra è sull'**inizio**, partenze e arrivi
    sono **due domande** e la stessa sessione può tornare da tutte e due (si conta per `Id`), una risposta con esattamente `Limit`
    sessioni può essere tagliata, mai due domande per scalo insieme;
  - ⚠️ nei test d'integrazione il client delle fixture risponde anche attraverso la sorgente: una VID senza `tracker-sessions-{vid}.json`
    non ha sessioni, uno scalo senza `tracker-airport-{icao}.json` nemmeno;
  - ⚠️ gli scali, le revisioni del piano di volo e il JSON grezzo **non** ci sono: se E13a ne ha bisogno, è un campo in più qui, con una
    nota (una fase del nucleo), non una lettura del client.

### Che cosa ha lasciato E17 (15–16 ottobre 2026, branch `m4/e17-in-person-activities`, PR #271; nata da `main` a `c232448`, in coda dopo la #268 di E12 e la #269 di E10s, con le loro teste `1887c27` e `00b0609` unite, `main` a `4ad747a`)

- **Che cosa c'è** (il dettaglio in `10`, E17, «Com'è andata»; **una migrazione**, `AddInPersonActivities`, l'ultima del contesto dopo
  quella di E12; niente del nucleo — solo `schema.d.ts` generato, `docs/FORKING.md` e le colonne di persona in `ErasureTests` —; una nota
  nuova, `2026-10-15-le-attivita-e-la-conservazione-breve`, **decisa** da Carmine sull'issue #267): la seconda e ultima fase di **M4c**.
  - **Le tabelle**: `evt_activities` (`EventActivity`, `EventActivity.cs`: area `EventBookings`, i turni calcolati da finestra e durata,
    `Turns`, `IsTurn`) ed `evt_activity_bookings` (`EventActivitySeat`, la riga del membro, `[WithdrawnByStakeholder]`).
  - **Il posto**: `Registrations/MemberSeats.cs` — l'iscrizione e poi l'attività `FOR UPDATE`, in `READ COMMITTED`, i posti contati sotto il
    secondo blocco —; `RegistrationSeats` (blocca l'iscrizione, legge i posti di un membro, li porta via con l'iscrizione), usato dal ritiro,
    da «Togli» e dalla cancellazione.
  - **Lo staff**: `Staff/EventActivityEndpoints.cs` (`/api/events/activities`), la lista dei posti `/api/events/seats`
    (`Registrations/StaffSeats.cs`, i nomi da `MemberNames`), le schede «Attività» e «Posti» (`screens/activities.tsx`).
  - **Il membro**: i turni con i posti liberi nella sezione «Iscrizione» della pagina dell'evento, e i suoi turni in `/events/mine`
    (`screens/ActivityTurns.tsx`); la mail `registrationReceived` con i turni.
  - **«Duplica»**: domande (con le chiavi) e attività (`EventCopy.Question`, `EventCopy.Activity`).
  - **`events-retention`** (`EventsRetentionJob.cs`, una volta al mese in UTC) e `Registrations/InPersonRetention.cs` (quando un evento è
    oltre la conservazione, che cosa tengono le statistiche, come le liste le rileggono).
  - **I test**: integrazione `EventsActivitiesTests` (13 dopo la revisione, la corsa sull'ultimo posto, quelle del ritiro e dell'eliminazione e l'audit della conservazione fra loro; VID **761150–761155**); unità
    `EventsActivitiesTests` (7); il giro `web/e2e/full/events-m4c-round.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **Le tre domande della nota sono decise** ([la risposta di Carmine sull'issue #267](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267#issuecomment-6215521232)):
    (A) l'area `EventBookings`, (A) un'attività con posti non si elimina — com'è nel codice —, e **(B)** per l'audit: la fase del nucleo
    **E10s** (`m4/e10s-retention-in-the-audit`) dà la chiamata che svuota nell'audit le colonne `[ErasedWith]` delle righe tolte per
    conservazione; E17 si mette in coda dopo di lei e la chiama da `EventsRetentionJob` per le iscrizioni (`AnswersJson` è già segnata),
    nella stessa transazione della cancellazione; non per i posti, che non hanno colonne segnate (la chiamata li rifiuterebbe). ⚠️ **E15b**,
    quando aggiunge le prenotazioni dei piloti alla conservazione, fa lo stesso per `EventBooking` (le sue cinque colonne sono segnate da E8b).
  - **E13a** (le statistiche): `evt_event_stats` ha già, per un evento in presenza oltre la conservazione, `registrations`, `answer_sums`
    (i numeri di `QuestionSum`) e `activity_seats` (`{ "<id attività>": posti }`), scritti da `events-retention`; E13a scrive il resto e
    **non tocca quelle tre colonne** di un evento in presenza (o le ricalcola dalle righe finché ci sono). La scheda «Statistiche» le mostra.
  - **E15b** (la conservazione di M4b): `events-retention` c'è; le prenotazioni e i PIREP dei piloti dopo `pilotRetentionMonths` sono un
    secondo passo dello stesso `RunAsync`, con la stessa regola di «finito» (`EventState.IsOver`).
  - ⚠️ **Un membro e i turni**: due turni che si toccano non si sovrappongono; il controllo è fra tutte le attività dell'evento, non fra
    eventi diversi.
  - **Dopo la revisione sulla #271** ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/271#issuecomment-6219791583), [Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/271#issuecomment-6219830164):
    sì alle letture 7, 11 e 13): le liste leggono le statistiche solo quando un evento non ha più iscrizioni né posti — **E13a**, che le
    scrive alla fine dell'evento, non cambia quello che lo staff legge prima del giro della conservazione —; «ridai» prende il blocco
    dell'iscrizione; un evento non si sposta oltre le sue attività; la conservazione salta un evento che fallisce.
  - ⚠️ **Il limite detto nella nota (lettura 11)**, accettato da Carmine come limite della 2A: lo staff che cambia un'attività controlla i posti senza blocco; un posto preso in
    quell'istante può restare su un turno che non c'è più, e ogni lettura lo regge.
  - ⚠️ **Su Windows** `Activities.tsx` e `activities.tsx` sono lo stesso file: il componente della pagina pubblica si chiama
    `ActivityTurns.tsx` per questo.
  - ⚠️ **I VID di prova**: E17 ha usato **761150–761155** della fascia 761150–761179 data da dalberone.

### Che cosa ha lasciato E10s (16 ottobre 2026, branch `m4/e10s-retention-in-the-audit`, PR #269, del nucleo; diretta su `main`, nata a `e041bb04`, con `main` a `3b8d7d7` unito, con E10p, poi a `4ad747a`, con E4d, E11b e il piano 1.34, e a `b5d3fb9`, con E12)

- **Che cosa c'è** (il dettaglio in `10`, E10s, «Com'è andata»; nessuna migrazione, niente nel browser; una nota nuova,
  `2026-10-16-la-conservazione-nell-audit`, **decisa** da Carmine sulla #267, [la risposta 3][a267s], la (B)):
  - **`AuditRedaction.EmptyDeletedAsync<TEntity, TKey>(DbContext context, IReadOnlyCollection<TKey> keys, CancellationToken)`**
    (`src/IvaoHub.Core/Privacy/AuditRedaction.cs`), accanto a `EmptyGoneAsync`: delle righe di `TEntity` con quelle chiavi che **non ci
    sono più**, svuota (diventano `null` nel JSON) le colonne segnate `[ErasedWith]` in **ogni** copia d'audit — `created`, i due lati
    di un `updated`, `deleted` —, di tutti i segni, chiunque la riga indicasse. Il resto della copia resta. Righe tracciate, **salva il
    chiamante**.
  - **Rifiuta** (`InvalidOperationException`; `ArgumentException` per le chiavi) un'entità che il contesto non mappa, una senza colonne
    segnate, chiavi di un altro tipo della chiave dell'entità, e una cancellazione di `TEntity` ancora da salvare nel contesto.
  - Un pezzo privato comune, `EmptyGoneCopiesAsync` (le chiavi che una riga ha ancora tolte, poi le copie in ordine), ora usato anche da
    `EmptyGoneAsync`: `ErasedAuditTests` passa com'era.
  - **I test**: `RetentionAuditTests` (integrazione, nuova, 3). Persone 780096 e 780097 di `ErasureTests`, solo nominate.
- **Che cosa deve sapere E17** (che unisce questo branch, poi `main` quando è unita):
  - **la chiamata**, in `events-retention`, per ogni entità cancellata, **dopo il salvataggio della cancellazione e nella stessa
    transazione**:

    ```csharp
    await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
    database.Registrations.RemoveRange(registrations);
    await database.SaveChangesAsync(cancellationToken);
    await AuditRedaction.EmptyDeletedAsync<EventRegistration, long>(database, [.. registrations.Select(row => row.Id)], cancellationToken);
    await database.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);
    ```

    (i nomi del `DbSet` sono quelli del contesto degli eventi). Così un errore riporta indietro insieme la cancellazione e lo
    svuotamento, e il giro dopo ritrova le righe; dopo un commit a sé un errore fra le due lascerebbe copie che nessuno cerca più;
  - ⚠️ **prima del salvataggio la chiamata è rifiutata**: le righe ci sarebbero ancora e non si svuoterebbe niente;
  - ⚠️ **un'entità senza segni è rifiutata**: `EventRegistration.AnswersJson` ha già `[ErasedWith(nameof(Vid))]` (E16); per i posti
    delle attività E17 sceglie quali colonne segnare (con la colonna di persona della stessa riga) e lo dice nella sua nota. Il segno
    vale anche alla cancellazione di una persona (E10n);
  - una chiave la cui riga c'è ancora è lasciata com'è; la chiamata è idempotente.
- **I tour** non ne hanno bisogno oggi (nota, §4): la loro conservazione cancella righe audite della forma del tour, e le righe di una
  persona che tocca non sono `[Audited]`.

[a267s]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/267#issuecomment-6215521232

### Che cosa ha lasciato E12 (15 ottobre 2026, branch `m4/e12-roster-publication`, PR #268; in coda dopo la #262 di E11b, nata dalla sua testa `973f385`, con la sua testa `12dfb0c` unita)

- **Che cosa c'è** (il dettaglio in `10`, E12, «Com'è andata»; **una migrazione**, `AddRosterPublicationAndTransfers`; niente del nucleo —
  solo `schema.d.ts` generato, le colonne di persona in `ErasureTests` e i due conteggi dei blocchi —; una nota nuova,
  `2026-10-14-la-pubblicazione-del-roster-e-la-cessione`, **decisa** da Carmine sulla #268 con due condizioni):
  - **La pubblicazione**: `Event.RosterPublishedAt` e `Atc/RosterPublication.cs`, la seconda metà di `events-roster`: una volta, a inizio −
    `rosterPublishDays`, e poi pubblicato per sempre; i turni detti con `notified_at`, una mail per controllore per evento.
  - **Le mail della correzione**: `Atc/ShiftNotices.cs`, chiamato dal `BeforeSave`, dall'`AfterSave` (dopo il commit di `ShiftLock`) e
    dalla `Delete` dei turni e delle postazioni: assegnato, cambiato, tolto, solo dopo la pubblicazione e solo a chi sapeva del turno.
  - **La pagina del roster** (`Public/PublicRoster.cs`, `screens/RosterPage.tsx`), **i turni del membro** (`Atc/MemberShifts.cs`,
    `screens/myShifts.tsx`), **la cessione** (`EventAtcTransfer`, `Atc/StaffTransfers.cs`, `Atc/TransferEndpoints.cs`,
    `screens/transfers.tsx`) e **il blocco** `events.atcCoverage` (`Atc/AtcCoverageProvider.cs`, `blocks/atcCoverage.tsx`).
  - **I test**: integrazione e unità `EventsRosterPublicationTests` (VID **761122–761128**; slug `evt-test-e12-…`); il giro
    `full/events-publication.spec.ts` (slug `evt-test-e2e-e12-…`).
- **Che cosa deve sapere la fase dopo**:
  - **E13a/E13b**: un turno ceduto e approvato è del nuovo titolare (`ControllerVid`), e la presenza si verifica su di lui; l'audit tiene chi
    l'aveva. **`events.staffQueue`** conta le cessioni da decidere come `events.atcCoverage` (`TransferStatus.Pending`, l'unico handler con
    `EventAtc.Edit` sulla richiesta). Le richieste in attesa di un evento iniziato si rifiutano soltanto.
  - **Dopo l'inizio** (la regola di Carmine sulla #262, che E11b scrive): un turno non si toglie, non cambia controllore né postazione, e se ne
    taglia solo la fine; il taglio è un «cambiato» per `ShiftNotices`, e la mail parte a chi sapeva del turno.
  - ⚠️ **Il banco non lancia un job**: la pubblicazione si guarda al quarto d'ora, come la proposta di E11b.
  - ⚠️ **I VID di prova**: dopo 761114–761121 di E11b, E12 ha usato **761122–761128**.

### Che cosa ha lasciato E11b (13 ottobre 2026, branch `m4/e11b-roster-proposal`, PR #262; in coda dopo la #256 di E11a, nata dalla sua testa `c967bc1`, con la sua testa `ac52c4b` e `main` a `cea6244` uniti)

- **Che cosa c'è** (il dettaglio in `10`, E11b, «Com'è andata»; **una migrazione**, `AddAtcShifts`; niente del nucleo — solo `schema.d.ts`
  generato e le colonne di persona in `ErasureTests` —; una nota nuova, `2026-10-13-la-proposta-del-roster`, **Proposta**):
  - **La tabella**: `evt_atc_shifts` (`EventAtcShift`): il controllore su una postazione da quando a quando, `origin` (`Proposed`, `Staff`),
    il perché in `reason` (JSON, `ShiftReason`), `notified_at` (`[NotAudited]`, per E12), l'esito (`attendance`, `attendance_by`,
    `attendance_at`, `attendance_note`, per E13); il FIR **copiato dalla postazione**; stakeholder il controllore.
  - **Il proponente** puro (`Atc/RosterProposer.cs`), i dati (`Atc/RosterData.cs`), la proposta di un evento sotto il blocco della sua riga
    (`Atc/RosterProposal.cs`) e il job **`events-roster`** (`Atc/EventRosterJob.cs`, ai minuti 3/18/33/48 UTC).
  - **La correzione**: i turni con il motore (`/api/events/atc/shifts`, `Atc/ShiftEndpoints.cs`) e **il roster** per postazione e ora, con gli
    scoperti, il perché e gli avvisi (`GET /api/events/atc/events/{id}/roster`, `Atc/StaffRoster.cs`); la sezione «Roster» del pannello
    dell'ATC e il form del turno (`screens/roster.tsx`, `screens/rosterWords.ts`, `screens/candidates.tsx`).
  - **I test**: unità e integrazione `EventsRosterTests` (VID **761114–761121**; slug `evt-test-e11b-…`); il giro `full/events-roster.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **E12** (la pubblicazione, la seconda metà di `events-roster`): il job è già registrato e decide dai suoi dati; la pubblicazione è un'altra
    condizione nello stesso giro (inizio − `rosterPublishDays`, `notified_at` per turno). ⚠️ **Le mail dopo la pubblicazione** partono dai
    verbi della correzione, che oggi sono del motore: l'`AfterSave` della risorsa dei turni (per «aggiungi» e «sposta») e la `Delete` di
    `ShiftEndpoints` (per «togli»), mai un verbo nuovo a mano. ⚠️ **L'`AfterSave` dei turni è già `CommitAsync`**, che chiude la transazione
    del blocco della riga dell'evento (`Atc/ShiftLock.cs`, il punto 1 della revisione della #262): una mail va **dopo** il commit, nello
    stesso hook, mai prima. **`events.atcCoverage`** conta gli scoperti come li conta il roster:
    `StaffRoster.Gaps` sui pezzi di `RosterProposer.Pieces`, gli scoperti non sono righe. **La pagina `/events/{slug}/roster`** e i turni in
    `/events/mine` leggono `evt_atc_shifts` (i nomi con `MemberNames`, come il roster dello staff). La cessione (`evt_atc_shift_transfers`)
    cambia il titolare di un turno: `ControllerVid` e `Origin = Staff`, come «sposta».
  - **E13a/E13b**: `attendance` è `Unknown` su ogni turno; **la penalità** si legge già da `Attended` e `NoShow` degli altri eventi
    (`RosterData.PenaltiesAsync`), con il peso 1: `noShowWeight` di E13b va passato a `RosterProposer.Penalty`. Un turno di un evento finito
    non si cambia né si toglie dai verbi della correzione: il no-show confermato è un verbo di E13b.
  - **E15b**: la cancellazione dei turni è già in `EventsPersonalData`.
  - ⚠️ **Il banco non lancia un job**: una spec non aspetta `events-roster` (come il promemoria di E6b); la proposta si guarda sul banco della
    fase al giro del quarto d'ora.
  - **Le condizioni di Carmine sulla #259** (nel codice): proposto il roster, le disponibilità restano chiuse anche se l'evento si sposta
    (`AtcRules.IsClosedForGood`); dall'inizio un turno non si toglie, tiene controllore, postazione, inizio e ore passate, e se ne taglia solo
    la fine, ad adesso o dopo (`shiftAfterStart`, la risposta di Carmine sulla #262: il sostituto è un turno nuovo dal taglio); un controllore su due postazioni dello stesso evento alle stesse ore è un rifiuto (`controllerBusy`), in altri
    eventi un avviso. **Le righe del roster e le postazioni dicono se il lettore può scriverle** (`Editable`, l'unico handler): una pagina
    nuova dell'ATC chiede quello, non `holdsPermission`. `isClosed` e `hasStarted` in `screens/atcPaths.ts`.
  - ⚠️ **`atcData` di IT**: Carmine ha risposto (A) sull'[issue #259](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259#issuecomment-6146395689) — `vipi` prima del primo roster vero, quando l'utente di sola lettura della vista esiste sull'host —; fino ad allora resta `none`: il roster si propone senza l'esperienza, e il
    perché di ogni turno lo dice («esperienza non contata»).
  - ⚠️ **I VID di prova**: dopo 761106–761113 di E11a, E11b ha usato **761114–761121** (e **761199** come nessuno che l'hub conosce).

### Che cosa ha lasciato E4d (14–16 ottobre 2026, branch `m4/e4d-events-adopt-core`, PR #263, in fondo alla coda #263 → #262 → #268; da `main` a `c232448`, con `main` a `e041bb04` e poi a `3b8d7d79` unito)

- **Che cosa c'è** (il dettaglio in `10`, E4d, «Com'è andata»; nessuna migrazione, nessuna decisione nuova; la nota
  `2026-10-14-gli-eventi-sul-lettore-dei-nomi`, **decisa** dalla regola di Carmine sulla #258, perché la PR tocca due test del nucleo):
  - **`EventsPeople.NamesAsync`** è una riga, `MemberNames.ReadAsync` del nucleo (E10m); `AtcRatingsAsync`, `Member` ed `EventMemberDto`
    restano del modulo. In `MemberNamesTests` resta solo la riga dei tour.
  - **`events-release` dice UTC** (`EventReleaseJob.TriggerName`, lo stesso nome di prima), come `events-reminders`; i due trigger li
    prova `tests/IvaoHub.UnitTests/EventsJobTriggersTests.cs`.
  - **I dialog di uno slot e dell'ora privata** (`EventSlots.tsx`, `PrivateSlots.tsx`) sono il `Dialog` del nucleo (E10l): la «X» dice
    «Chiudi» e «Close». La guardia di `Dialog.test.tsx` non ha più eccezioni.
  - **Le schede «Domande» e «Iscrizioni»** di E16, vuote, dicono `events:questions.empty` ed `events:registrations.empty`, non più la
    frase del nucleo su un dipartimento; i test in `titlesAndEmptyLists.test.tsx`.
- **Che cosa deve sapere la fase dopo**:
  - **Ogni nome per VID** si legge con `EventsPeople.NamesAsync` o con `MemberNames.ReadAsync`: sono la stessa cosa. Un VID sotto uno
    non ha mai un nome.
  - **Un job nuovo degli eventi** (`events-roster`, `events-after`, `events-digest`, `events-retention`) dice il suo fuso nel trigger e
    aggiunge la sua riga a `EventsJobTriggersTests`. ⚠️ Un trigger senza fuso prende quello della macchina: su una macchina il cui fuso
    locale si chiama `UTC` il test passerebbe anche senza; la prova al contrario l'ho fatta qui, in un fuso europeo (cade con
    «Expected: "UTC" — Actual: "W. Europe Standard Time"»), come per `TrainingJobTriggersTests`.
  - **Un dialog nuovo** è il `Dialog` del nucleo (`../../../shared/ui`); quello di Atmosphere fa cadere la guardia, ora senza lista.
  - **Una lista generata nuova** dice la sua `emptyDescription`, parole che descrivono (E4c). ⚠️ Quella delle postazioni di E11a
    (`atc.positions.empty`) invita («aprine una»), e chi legge l'ATC soltanto non ha il bottone: da riscrivere, con E11b o dopo.
  - **E10p** (#261, le ore a 24 in un formatter solo) non era unita: quando entra, gli eventi la prendono in una fase loro, se la sua
    nota lo chiede.
  - ⚠️ **E11b (#262)** è aperta accanto e tocca forse `EventsModule.cs`, `EventsPeople.cs` e i file di lingua: i conflitti all'unione
    sono il passo della coda del master.
  - **Con `main` a `e041bb04`** (16 ottobre) entra E10q: il Pkcs 10.0.13 e la versione 0.6.8. Niente degli eventi cambia; i conti
    dopo l'unione sono in `10`, E4d, «Com'è andata».
  - **Con `main` a `3b8d7d79`** (16 ottobre) entra E10p: le ore del nucleo in 24 con `formatMoment`. I dialog e le schede di E4d le
    scrivono con `useMoment` o con il `DataList` del nucleo, e non cambiano; la coda è **#263 → #262 → #268**, E4d in fondo.

### Che cosa ha lasciato E10p (13–16 ottobre 2026, branch `m4/e10p-times-24-hour`, PR #261, del nucleo, senza coda; da `main` a `cea6244`, con `main` a `c232448` e poi a `e041bb0` unito)

- **Che cosa c'è** (nota `decisions/2026-10-13-le-ore-in-24.md`, **decisa** da Carmine sull'issue #260; il dettaglio in `10`, E10p,
  «Com'è andata»). Tutto nel browser, niente in C#:
  - **`formatMoment(locale, value, options)`** in `web/src/shared/i18n/useMoment.ts`: il corpo di `useMoment`, che ora lo chiama. È
    **l'unico posto** che scrive un istante, e le 24 ore (`hour12: false`) sono una riga lì.
  - I quattro formattatori del nucleo che in inglese scrivevano «7:00 PM» ci passano sopra: `localTime` (`SchemaForm.tsx`) e `DateCell`
    (`DataList.tsx`) con `formatMoment`; `MessageThread.tsx` e `routes/_staff/staff.$dept.contacts.$id.tsx` con `useMoment`. Ognuno
    tiene il suo aspetto.
  - **I test**: `web/src/shared/i18n/twentyFourHours.test.tsx` (6). Tre valori attesi di due test di Carmine cambiano con la sua
    risposta (`back-office.spec.ts:281`, `extensions.test.tsx:168–169`).
- **Che cosa devono sapere le fasi dopo**:
  - ⚠️ **Mai un `new Intl.DateTimeFormat` con un `timeStyle` o un `hour` suo**: un componente scrive un istante con `useMoment`, una
    funzione che riceve la lingua (una cella, un aiuto di una pagina) con `formatMoment`. `docs/UI-GUIDELINES.md`, «Times», lo dice.
  - I moduli eventi e formazione scrivono già le ore con `useMoment`: nessuna fase di modulo dopo questa.
  - ⚠️ **Un test che legge un'ora in inglese** scrive `19:00`, mai `7:00 PM`; e attenzione alle sottostringhe: `19:00` non contiene
    `7:00`, `18:00` contiene `8:00`.

### Che cosa ha lasciato E10q (15 ottobre 2026, branch `m4/e10q-pkcs-advisory`, PR #265; diretta su `main`, nata a `c232448`)

- **Che cosa c'è** (il dettaglio in `10`, E10q, «Com'è andata»; nessuna migrazione, nessun codice; una nota nuova,
  `2026-10-15-il-pkcs-corretto`, **decisa** con l'issue #264): `System.Security.Cryptography.Pkcs` **10.0.13** in
  `Directory.Packages.props` e un riferimento diretto in `IvaoHub.Core.csproj`, l'unico progetto che lo porta (MailKit → MimeKit 10.0.0);
  `<Version>` **0.6.8**.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **un `dotnet restore` locale può passare anche quando quello della CI fallisce**: senza modifiche ai progetti il restore è «già
    aggiornato» e non rilegge gli avvisi. Per vederli: `dotnet restore IvaoHub.sln --force --no-http-cache`, poi
    `dotnet list IvaoHub.sln package --vulnerable --include-transitive`;
  - ⚠️ un avviso su un pacchetto **transitivo** si corregge così, con una `PackageVersion` nel gruppo «Security fixes of transitive
    packages» e un riferimento diretto nel progetto che lo porta (`dotnet nuget why <progetto> <pacchetto>`), **mai** con `NoWarn` o
    `NuGetAudit=false`; il riferimento a Pkcs se ne va quando MimeKit ne porterà uno corretto da sé;
  - il runtime incluso nel pacchetto self-contained è quello dell'ultimo SDK 10.0 che `setup-dotnet` installa (oggi 10.0.402, runtime
    10.0.13); il `Pkcs.dll` pubblicato è comunque quello del pacchetto (nota, §4).

### Che cosa ha lasciato E11a (12 ottobre 2026, branch `m4/e11a-positions-and-availability`, PR #256; in coda dopo la #255 di E16, nata dal suo branch a `dd5b263`, con la sua testa `78fc9b6` unita)

- **Che cosa c'è** (il dettaglio in `10`, E11a, «Com'è andata»; **una migrazione**, `AddAtcPositionsAndAvailability`; niente del nucleo —
  solo `schema.d.ts` generato, `docs/FORKING.md` e le colonne di persona in `ErasureTests` —; una nota nuova,
  `2026-10-12-le-postazioni-e-la-disponibilita`, **Proposta**, con la domanda a Carmine sulla PR): la prima fase di **M4b**.
  - **Le tabelle**: `evt_atc_positions` (`EventAtcPosition`: `IHasFir`, area `EventAtc`, il FIR copiato dalla directory dal server prima del
    permesso), `evt_atc_availability` (`EventAtcAvailability`, univoca su `(event_id, controller_vid)`) e `evt_atc_availability_windows`
    (`EventAtcWindow`, le finestre), tutte e due righe del membro con **`[WithdrawnByStakeholder]`**. **I grant `firTeam` del seme su
    `EventAtc.*` prendono effetto** all'avvio, ora che l'area ha una riga con il FIR: niente più l'avviso «not applied» a ogni avvio.
  - **Lo staff**: le postazioni (`/api/events/atc/positions`, lista e form generati) nella scheda «ATC» dell'evento e, per chi tiene l'ATC
    senza gli eventi, nella sezione **«ATC degli eventi»** (`/staff/events/atc`, `screens/atc.tsx`, `atcPaths.ts`); sulla pagina di una
    postazione chi è disponibile (`/api/events/atc/positions/{id}/candidates`, `Atc/StaffAvailability.cs`).
  - **Il membro**: la disponibilità con le finestre e una nota dalla pagina dell'evento (`screens/Availability.tsx`, `Atc/MemberAvailability.cs`,
    `/api/events/mine/availability`), fino a inizio − `applicationsCloseDays`, con il rating minimo; le regole in `Atc/AtcRules.cs`.
  - **Il form dell'evento** ha «Roster ATC» (preimpostato dal tipo) e la durata del turno; **le impostazioni** hanno i sette numeri dell'ATC;
    **«Duplica»** copia le postazioni; **`EventsPersonalData`** cancella o tiene senza nota le disponibilità.
  - **I test**: integrazione `EventsAtcTests` (8; VID **761106–761113**, una fascia nuova data da dalberone; slug `evt-test-e11a-…`); unità
    `EventsAtcRulesTests`; vitest `Availability.test.tsx`; il giro `web/e2e/full/events-atc.spec.ts` (slug `evt-test-e2e-e11a-…`, il trainer
    del banco con un grant a VID sull'ATC al posto dell'AOD).
- **Che cosa deve sapere la fase dopo**:
  - **E11b** (il roster): i turni a pezzi di `shift_minutes` dell'evento, o dell'impostazione (`AtcEventDto.ShiftMinutes` lo dice già);
    «disponibile per tutto il turno» è **`AtcRules.Cover`** sulle finestre del controllore; le ore di una postazione sono `FromUtc`–`ToUtc`, e
    una postazione può comparire due volte in un evento, in ore che non si sovrappongono. Il rating di un controllore lo dà
    `EventsPeople.AtcRatingsAsync`; il minimo della postazione è `IAtcPositionDirectory.MinimaAsync` (E10c). `evt_atc_shifts` è `IHasFir`
    come la postazione: il FIR **si copia dalla postazione**, non dalla directory. **L'eliminazione di una postazione** oggi è libera: con
    i turni, una postazione con turni non si elimina (o se ne vanno i turni: è una scelta di E11b). L'interruttore del roster già non si
    spegne con postazioni o disponibilità: aggiungere i turni a `EventSaving`.
  - **I candidati di una postazione** (la risposta 2 di Carmine, la (B)): `AtcCandidateDto` dice da quando a quando il controllore è
    disponibile **dentro la postazione** (`AvailableFromUtc`, `AvailableToUtc`), `WholePosition` e quante finestre la toccano, non le
    finestre una per una; un buco fra due finestre si vede solo come «non per tutto il tempo». Il roster di E11b, per postazione e ora,
    mostra la copertura vera. ⚠️ **Un evento annullato o finito** non prende postazioni: il server le rifiuta e la pagina non offre
    «Salva» (`isClosed` in `screens/atcPaths.ts`); i turni di E11b faranno lo stesso. ⚠️ **Il FIR di una postazione** resta quello con
    cui il nominativo è stato scritto, anche dopo «Duplica», finché il nominativo non si riscrive.
  - **E12**: le disponibilità in `/events/mine` (lettura 8 della nota); `MemberAvailability.MineAsync` c'è già.
  - **E15b**: la cancellazione delle disponibilità è già in `EventsPersonalData`; restano i turni.
  - **E10n** (#251) ed **E10m** (#252), in coda prima di questa PR, sono entrate con il merge della testa di E16: la nota di una
    disponibilità porta **`[ErasedWith(nameof(ControllerVid))]`** (una disponibilità ritirata prima della cancellazione non lascia la nota
    nell'audit; provato in `EventsAtcTests`); `EventsPeople` passa al lettore dei nomi del nucleo nella piccola fase degli eventi che E10m
    annuncia — il rating ATC accanto al nome (`AtcRatingsAsync`) resta del modulo.
  - ⚠️ **Il banco**: un grant a un VID va solo a chi è già entrato (`errors.grant.notStaff`), e lo fa rientrare. ⚠️ **`api.ts`**: una chiave
    di query uguale a un tipo del calendario del seme (`'event'`) fa cadere `EventsArchitectureTests`.
  - ⚠️ **I VID di prova**: dopo 761101–761105 di E16, E11a ha usato **761106–761113**, dati da dalberone il 12 ottobre.

### Che cosa ha lasciato E16 (12 ottobre 2026, branch `m4/e16-in-person-registration`, PR #255; nata in coda dopo la #249 dal branch di E8b a `4bc847e`, ora in coda dopo la #254 di E10o, con la sua testa `a97a1fd` unita)

- **Che cosa c'è** (il dettaglio in `10`, E16, «Com'è andata»; **una migrazione**, `AddInPersonRegistration`; niente del nucleo — solo
  `schema.d.ts` generato —; una nota nuova, `2026-10-12-le-domande-e-l-iscrizione`, **decisa** da Carmine sull'**issue #253** — (A) a tutte e due
  le domande —): la prima fase di **M4c**, scelta da dalberone prima di M4b.
  - **Le tabelle**: `evt_questions` (`EventQuestion`: ordine, tipo dall'elenco chiuso, domanda e scelte tradotte, obbligatoria; le scelte
    con una chiave che il server dà e che non cambia), `evt_registrations` (`EventRegistration`: il membro in `vid`, le risposte in JSON per
    `q{id}`, `created_at`, `updated_at`; univoca su `(event_id, vid)`, `[WithdrawnByStakeholder]`), ed **`evt_event_stats` intera**
    (`EventStats`, tutte le colonne del design §1.10: **E13a non la migra**; in E16 nessuno la scrive).
  - **Il form dell'evento** ha «In presenza» e il luogo (`EventWriteDto` in coda, con un predefinito, `presetSwitches`); il luogo serve per
    pubblicare un evento in presenza, e sta nella testa della pagina (un fatto in più di `EventHead`).
  - **Le domande**: `/api/events/questions`, una risorsa del motore (`Staff/EventQuestionEndpoints.cs`), la scheda «Domande» con **le
    somme** nella lista (`Registrations/RegistrationSums.cs`), ogni domanda in un form generato (`screens/questions.tsx`).
  - **L'iscrizione**: le risposte controllate per tipo in un posto solo (`Registrations/RegistrationAnswers.cs`), i verbi del membro
    (`Registrations/MemberRegistrations.cs`, `GET`/`PUT`/`DELETE /api/events/mine/registrations…`), la scheda «Iscrizioni» con «Togli» e
    il motivo (`Registrations/RegistrationEndpoints.cs`, `StaffRegistrations.cs`, `screens/registrations.tsx`), la sezione della pagina
    dell'evento (`screens/Registration.tsx`, il form costruito dalle domande in `screens/registrationForm.ts`) e `/events/mine`.
  - **Le mail**: `events.registrationReceived` a ogni iscrizione e cambio, `events.registrationRemoved` con il motivo; annullare e cambiare
    gli orari avvisano anche gli iscritti (`EventSaving.PeopleToTellAsync`).
  - **`EventsPersonalData`** per le iscrizioni; **`inPersonRetentionMonths`** nelle impostazioni.
  - **I test**: integrazione `EventsRegistrationsTests` (9 dopo la revisione; VID **761101–761105**, una fascia nuova data da dalberone;
    slug `evt-test-e16-…`); vitest `registrationForm.test.ts`, `answers.test.ts`; il giro `web/e2e/full/events-in-person.spec.ts` (slug
    `evt-test-e2e-e16-…`).
- **Che cosa deve sapere la fase dopo**:
  - **Le due domande dell'issue #253 sono decise** ([le risposte](https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540)):
    le etichette del form del membro vengono dal nucleo, con la fase **E10o** (un campo porta il suo nome nello schema), e **E16 è in coda
    dopo la sua PR**; «Togli» un'iscrizione chiede il motivo e manda `events.registrationRemoved`, com'è scritto.
  - **E17** (le attività, la conservazione, «Duplica»): `RegistrationRules.IsOpen` e `IsWithdrawable` dicono fino a quando; un posto di
    un'attività lo prende solo chi ha una riga in `evt_registrations`; **`registrationReceived` aspetta i turni** (il design: «con risposte e
    turni delle attività»), un dato in più di `EventsMail.RegistrationReceivedAsync`; **la conservazione** scrive le somme in
    `evt_event_stats` (`RegistrationSums.Of`, la colonna `answer_sums`) prima di cancellare le iscrizioni; **«Duplica»** copia le domande
    (non le iscrizioni) — le chiavi delle scelte com'erano.
  - **E10n** (#251, nella coda sotto): `EventRegistration.AnswersJson` porta `[ErasedWith(nameof(Vid))]`, così le risposte di un'iscrizione
    ritirata o tolta prima della cancellazione escono anche dall'audit; `EventsRegistrationsTests` lo prova con un'iscrizione ritirata.
  - **Dopo la revisione sulla #255** ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6130535660),
    [la risposta di Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6131119914): sì alle letture 15, 20, 4 e
    12, no alle parole delle liste scritte dal server). ⚠️ **Le liste ricevono dati, la pagina scrive le parole nella lingua sullo
    schermo**: `EventQuestionDto.Sum` (numeri per chiave), le risposte com'erano date in `EventRegistrationDto` e in `MyRegistrationDto` (che
    porta anche le domande del suo evento), e `screens/answers.ts` che le scrive; **le mail** tengono le parole del server
    (`RegistrationAnswers.Lines`), nella lingua di chi le riceve. Una fase che mette un valore in parole in una lista fa lo stesso (E17: i
    posti delle attività). **Dopo la seconda lettura** ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6146420615),
    [Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/255#issuecomment-6146461872): sì alla lettura 11 com'è ora — la seconda
    iscrizione dello stesso istante diventa un cambio della prima): l'eraser tiene una scelta solo se nomina scelte della domanda
    (`RegistrationAnswers.Counted`), e la scheda «Iscrizioni» dice l'errore della lettura delle domande. ⚠️ **La cancellazione** tiene di
    un'iscrizione finita solo le risposte contate alle domande che ci sono, e marca
    sempre la riga come cambiata, così l'audit perde le sue copie di prima: una tabella nuova di E17 (i posti) che tiene righe con lo
    pseudonimo guarda lo stesso caso. ⚠️ **Ogni lettura salta un valore che non torna con la sua domanda** (`RegistrationAnswers.Fits`, e in
    TS `answerLines`).
  - ⚠️ **Sul banco** le persone leggono l'italiano anche con la pagina in inglese (`readInEnglish` mette solo il cookie): le **mail**
    arrivano in italiano, le pagine no.
  - ⚠️ **Due test del modulo di fasi prima, cresciuti con la forma** (detto nella PR): due righe dell'anteprima in `EventsErasureTests`
    (E8b); `inPerson` e `venue` in `eventForm.test.ts` (E3a); i tre campi nuovi di `PublicEventDto` nelle fixture (`EventHead.test.tsx`,
    `public.test.tsx`, lo smoke `events-public.spec.ts`) e `inPersonRetentionMonths` in `schemas.test.ts`. Una fase che aggiunge un campo
    obbligatorio fa lo stesso.
  - ⚠️ **I VID di prova**: la fascia del modulo è finita; E16 ha usato **761101–761105** (dopo 761100 di E10n), dati da dalberone.

### Che cosa ha lasciato E10o (12 ottobre 2026, branch `m4/e10o-field-names-in-schema`, PR #254, del nucleo, in coda dopo la #239; con la testa di E10j `0963908`)

- **Che cosa c'è** (nota `decisions/2026-10-12-un-campo-dice-il-suo-nome.md`, **decisa** da Carmine sull'issue #253, [la sua
  risposta][a253o], punto 1: A, prima una fase del nucleo; non B, i file di lingua non tengono righe; il dettaglio in `10`, E10o, «Com'è
  andata»). Tutto nel browser, niente in C#:
  - **Un campo dice il suo nome**: `.meta({ label })` sullo schema di un campo (`FieldMeta.label`, `web/src/shared/forms/schema.ts`),
    una stringa **già nella lingua sullo schermo**, come le etichette di `choices`. `SchemaForm` la disegna al posto di
    `<labels>.fields.<path>`, per ogni tipo di campo: la riga, l'interruttore, il gruppo di caselle (il suo `aria-label`), il fieldset,
    le schede di lingua. Senza, tutto com'era.
  - **Il «nessuno» del nucleo**: una select opzionale di un campo con un nome suo dice `form.none` («No choice» / «Nessuna scelta»);
    gli altri campi la loro `<labels>.options.<path>.none`. Vale per la select di `choices`, quella di una `z.enum` e la griglia delle
    icone.
  - **I test** (vitest, 10): `web/src/shared/forms/SchemaForm.label.test.tsx`, con lo schema del form di E16. `docs/UI-GUIDELINES.md`
    dice l'annotazione in inglese.
- **Che cosa devono sapere le fasi dopo**:
  - **E16** (il form del membro, `web/src/modules/events/screens/registrationForm.ts` sul suo branch) si mette in coda dopo questa e
    passa a ogni campo `.meta({ label: <il testo della domanda nella lingua sullo schermo> })`; l'oggetto `answers` tiene il suo nome
    i18n. Una domanda non obbligatoria di una scelta è `.optional()`, e dice «Nessuna scelta» da sé.
  - **Un form costruito da righe** (E17 con le attività, il form di un fork) fa lo stesso: mai le parole di una riga in i18n a runtime
    (Carmine, #253).
  - **Uno schema scritto nel codice non porta `label`**: i suoi nomi sono chiavi, e `DataList` fa le intestazioni di colonna dalle
    stesse chiavi.
  - ⚠️ **Il suggerimento sotto un campo resta in i18n** (`<labels>.hints.<path>`): un form costruito da righe che ne volesse uno
    aggiunge l'annotazione accanto a `label`, con una nota.
  - ⚠️ **Le opzioni di una `z.enum` restano chiavi** anche su un campo con un nome suo: un insieme che viene da righe è `choices` con
    `{ value, label }`.

[a253o]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/253#issuecomment-6120877540

### Che cosa ha lasciato E10j (9–12 ottobre 2026, branch `m4/e10j-jobs-catch-up`, PR #239, del nucleo; ultima della coda #242 → #250 → #252 → #251 → #249 → #239, con la testa di E8b `889ba1a` unita, `main` ad `a75ed6c`)

- **Che cosa c'è** (nota `decisions/2026-10-09-i-job-che-recuperano.md`, **decisa** da Carmine sulla #239: l'ultimo giro *finito*, il
  blocco del database, i job dovuti uno dopo l'altro, il blocco che si apre accettato, e il token anche nell'indirizzo; il dettaglio
  in `10`, E10j, «Com'è andata», «Dopo la revisione» e «Dopo la risposta 5»):
  - **Ogni job Quartz dell'hub gira una volta per occorrenza del suo cron, qualunque processo sia vivo**, e nessun job cambia:
    `src/IvaoHub.Core/Jobs/ScheduledJobs.cs` è un `ITriggerListener` su tutti i trigger che, prima di ogni giro, prende il blocco
    del job nel database (`hub-job:<database>:<job>`, `JobLocks`, una connessione per processo fuori dal pool) e rilegge l'ultimo giro
    **finito** in `hub_jobs_log`: un altro processo che lo fa, o un giro che ha già coperto l'occorrenza, e il giro salta.
  - **Il recupero** (`JobCatchUp`): cinque secondi dopo l'avvio e ogni minuto fa **un giro** sui job dovuti (`JobSchedule.IsDue`:
    un'occorrenza del cron, nel fuso del trigger, dopo l'inizio dell'ultimo giro finito) e li lancia **uno dopo l'altro**; un giro alla
    volta per processo. **Acceso per difetto solo in `Production`** (`Jobs:CatchUp`): sul banco e nei test d'integrazione i job girano
    alle loro ore come prima.
  - **`/api/jobs/run`** per l'operazione pianificata dell'host, con il token `Jobs:Token` (almeno 32 caratteri, dai segreti)
    **nell'intestazione** (`POST`, `Authorization: Bearer …`) o, per un pannello che sa solo recuperare un indirizzo, **nell'indirizzo**
    (`GET …?token=…`, la risposta 5): si unisce al giro e risponde quando è finito, al più dopo `Jobs:WaitSeconds` (80 se manca); 404
    senza token configurato, 401 senza o con un altro, 403 per un `POST` senza `Authorization` (il guardiano di `/api`), 429 oltre
    dieci chiamate al minuto dei due modi insieme (il limite del login). **Il token dell'indirizzo non va mai nel log dell'hub**:
    `JobTokenLogMask` (progetto Web) lo scrive `token=***` nelle righe di ASP.NET Core di una richiesta. In `docs/DEPLOYING.md`, con il
    prezzo del ripiego: il token nei log del server web e dei proxy davanti.
  - **La coda delle mail** salva l'esito di ogni mail subito; **i fusi** del nucleo sono espliciti (la coda in UTC, i contorni dei FIR
    nel fuso della divisione).
- **Per chi scrive un job degli eventi** (il promemoria di E6b, `events-roster`, `events-after`, `events-digest`, `events-retention`):
  niente da chiamare. Registrato come gli altri (`AddJob<…>(job => job.WithIdentity(JobName))` e un trigger cron `.ForJob(JobName)`),
  il recupero e il blocco lo coprono, se: **ogni giro scrive la sua riga con `FinishedAt` su ogni uscita**, con `Job` uguale al nome
  del job in Quartz; **il trigger dice il suo fuso** (`InTimeZone`, il fuso della divisione con la pipeline delle opzioni come
  `training-expiry` quando l'ora conta per chi legge); **decide dai suoi dati** (la regola di sempre). Un riepilogo non scrive un suo
  «già mandato oggi»: è la riga del giro. ⚠️ Ma **un giro fermato a metà si rifà**: un job che manda mail con un salvataggio per mail
  (come il riepilogo dei tour) le rimanda; le mail di un giro in un salvataggio solo, come `AwardQueueMailJob`, o un segno per
  destinatario.
- ⚠️ **I fusi di `events-release` e `training-reminders`**: dicono UTC nella **prossima fase del loro modulo** (due righe in
  `EventsModule.cs` e in `TrainingModule.cs`; la regola 6 le tiene fuori da una PR del nucleo). Un quarto d'ora cade agli stessi istanti
  in ogni fuso, ma la nota del 28 settembre (§7) li vuole espliciti (il punto 6 della revisione della #239).
- ⚠️ **Che cosa offre l'operazione pianificata del pannello è un punto aperto: lo chiude chi la imposta, alla consegna** (il
  maintainer non ha accesso al pannello; risposta 5 sulla #239, nota §2): dove il pannello esegue un comando, l'intestazione; dove
  recupera solo un indirizzo, il token nell'indirizzo.
- ⚠️ **Flight Ops resta al maintainer** (nota §3): i fusi degli otto trigger dei tour (il riepilogo delle 07:00 nel fuso della
  divisione, UTC dove il commento dice UTC) e il riepilogo fermato a metà, che rimanda le mail già in coda (nota §1.7), sono una
  richiesta a una sua sessione; il resto lo copre il nucleo.
- ⚠️ **Quartz tiene uno scheduler per nome in un processo**: un test che avvia due host vivi insieme dà a ognuno un nome suo
  (`ScheduledJobsTests.Host`), o i due condividono lo scheduler del primo.
- ⚠️ **Un test che mette in pausa un job** (`TourTests`, `EventsLifeTests`, `AwardQueueMailTests`) resta com'è: il recupero salta i
  job in pausa, e nei test è spento.
- **Per provare che cosa scrive il log dell'hub**: il logger legge con `ReadFrom.Services` ogni `ILogEventSink` registrato nei servizi
  dell'host, quindi un test ne registra uno suo con `ConfigureTestServices` e riceve ogni evento dopo gli arricchitori dell'hub
  (`ScheduledJobsTests.LogCapture`); il livello di una categoria si abbassa con `Serilog:MinimumLevel:Override:<categoria>` nella
  configurazione del test.

### Che cosa ha lasciato E8b (11–12 ottobre 2026, branch `m4/e8b-duplicate-and-m4a-round`, PR #249; nata dal branch di E7 a `0d766fe`, ne ha unito la testa nuova `7a847a8`, ora in coda dopo la #251 con la testa di E10n `b4ea56e`, `main` ad `a75ed6c`)

- **Che cosa c'è** (il dettaglio in `10`, E8b, «Com'è andata»; **nessuna migrazione**, niente del nucleo — solo `schema.d.ts` generato —;
  due note nuove, **decise** da Carmine: `2026-10-11-duplica-e-la-cancellazione-negli-eventi`, con le sue risposte sull'**issue #248**
  (la (A′) e la (i)), e `2026-10-11-la-testa-della-pagina-di-un-evento`, con la sua risposta 1 sulla **#249** (la (A), con i nomi degli
  scali leggibili senza mouse)):
  - **«Duplica»**: `POST /api/events/events/{id}/duplicate` (`Staff/EventDuplication.cs`). La copia pura in `EventCopy` — l'evento con
    ogni sua impostazione, nella stessa cura, gli orari spostati della stessa differenza, una bozza; uno scalo, una rotta e uno slot
    pubblico com'erano, ai nuovi orari —, il piano letto prima di scrivere (`EventDuplicatePlan`: il banner solo se la libreria ha ancora il
    file, gli slot pubblici se chiesti, se generare i privati) e il salvataggio in una transazione (`SaveAsync`). **I privati si generano
    con la copia** (con `PrivateSlotGeneration` di E7) quando i suoi slot pubblici ci sono già — copiati, o l'evento non ne prende —, e
    altrimenti li fa «Genera» dopo la tabella nuova (la (A′)); quando il generatore li rifiuta la risposta lo dice
    (`EventDuplicatedDto.PrivateSlotsNotGenerated`, la chiave del rifiuto), e la pagina invece di aprire la copia dice perché, con
    «Apri la copia». Un nuovo inizio che sposterebbe un orario fuori dal calendario è rifiutato (`EventCopy.MovesWithinTheCalendar`).
    **Chi duplica**: `Events.Edit` sull'evento nuovo com'è prima di essere
    scritto (scope `events:event:0`: un grant su un evento solo non duplica), e il permesso dell'area di ogni riga che la copia scrive
    (`EventDuplicatePlan.AreasWritten`), chiesti all'unico handler prima di scrivere. Le regole dell'indirizzo sono scritte una volta,
    `EventAddressRules.EventAddress`, per il form e per «Duplica». La pagina: «Duplica» nella barra di un evento, in ogni stato
    (`EventDuplicatePage`, `/staff/events/$id/duplicate`, `duplicateSchema`).
  - **`EventsPersonalData`** (`src/IvaoHub.Modules.Events/EventsPersonalData.cs`): le prenotazioni della persona di un evento non ancora
    finito si cancellano, e lo slot torna libero; quelle di un evento finito restano con lo pseudonimo, senza `callsign`, `other_icao`,
    `other_time_utc`, `unflown_excused_note` e `flown_session_id`. **«Finito»** si legge in un posto solo, **`EventState.IsOver`** (la fine
    passata, annullato o no: la (i)), che ora usano anche `Of`, `IsSeen` e `Unseen`. Una prenotazione il cui evento non c'è più è lasciata
    fuori, come dall'anteprima. Che cosa dà l'esportazione dopo una cancellazione lo dice `docs/events-bookings-export.md`. **Le cinque
    colonne svuotate sono segnate** `[ErasedWith(nameof(BookerVid))]` su `EventBooking` (dopo E10n, la #251): l'audit di una prenotazione
    ritirata o tolta prima della cancellazione le perde anche lui.
  - **Il giro completo di M4a**: `web/e2e/full/events-round.spec.ts` (slug `evt-test-e2e-e8b-…`, voli `XEE811`–`XEE813`, `XEE821`–`XEE822`,
    `XEP901`), due test — il giro dell'RFO, dall'incolla a «Duplica», e l'RFE del mese scorso, finito, duplicato su una nuova data.
  - **`docs/FORKING.md`**: la sezione «The events module» — il dipartimento di base, i `positionGrants` dell'ED e di chi collabora, le
    impostazioni con `kindPresets` per prima, pagine, blocchi, mail e job, il Gate Manager, che cosa tiene e che cosa cancella, che cosa
    non fa ancora —, e lo stato in testa.
  - **La testa della pagina di un evento** (chiesta da dalberone dopo l'apertura della PR): sotto titolo e riassunto due righe corte —
    quando, in UTC e nell'ora della divisione; le prenotazioni come badge (aperte; dal momento, con il conto di E6b; chiuse), gli scali come
    codici, ognuno con il suo nome al passaggio del mouse, al focus e al tocco (**`Hint`**, esportato da `EventSlots.tsx` e non copiato),
    chi organizza solo quando non è la divisione —, senza il badge dello stato (il tipo resta); poi gli slot, i privati, in fondo la
    descrizione e le rotte. `screens/EventHead.tsx`, il badge in `BookingOpening.tsx`, i dati in `cards.ts` (`headSpan`, `headMoment`) e
    `bookingTimes.ts` (`bookingStage`). E `/events/mine` vuota dice «Nessuna prenotazione».
  - **I test**: unità `EventsDuplicationTests` (7); integrazione `EventsDuplicateTests` (3; VID **761063**, il coordinatore di E6b, e
    **761098** come collaboratore con i grant del test; scali `XEJ1`–`XEJ3`, tipi `XEJA`/`XEJB`, slug `evt-test-e8b-dup-…`) ed
    `EventsErasureTests` (2, il secondo una prenotazione ritirata prima della cancellazione; VID **761098**, la persona cancellata, e
    **761015**, il superadmin; slug `evt-test-e8b-erase-…`); un vitest in
    `schemas.test.ts`; per la testa `EventHead.test.tsx` (7), `public.test.tsx` (l'ordine della pagina) e i dati in `cards.test.ts` e
    `bookingTimes.test.ts`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Il buco dell'audit** (il punto 7 della revisione della #249, la risposta 2 di Carmine) è chiuso da **E10n** (**#251**, del nucleo,
    sotto nella coda) e dai segni di E8b sulle cinque colonne della prenotazione. **Una colonna nuova della prenotazione** che è un dato del
    pilota (M4b, M4c) porta lo stesso segno, `[ErasedWith(nameof(BookerVid))]`, e la sua riga nell'eraser se una prenotazione tenuta la
    deve perdere; `ErasedAuditTests` di E10n cade su un segno che non nomina una colonna di persona, e l'avvio pure.
  - **La risposta di «Duplica»** è `EventDuplicatedDto` (l'evento e `privateSlotsNotGenerated`), non più `EventDetailDto`: un client del
    verbo legge `event`.
  - ⚠️ **`PrivateSlotGeneration.GenerateAsync` dentro una transazione già aperta** (E8b, al merge della testa nuova di E7): blocca e
    scrive in quella, e il commit è di chi lo chiama — «Duplica» lo chiama così, per una copia che nessun altro vede ancora. Da solo
    apre la sua, `READ COMMITTED`, con la riga dell'evento `FOR UPDATE`. Chi lo chiama da dentro una transazione su un evento che altri
    vedono deve pensare al livello d'isolamento della sua.
  - **E4c (la #242) è sotto nella coda**: `public.tsx` ha i suoi metadati della pagina accanto alla testa di E8b, uniti da soli.
  - **`Hint`** resta un pezzo della pagina di un evento (`EventSlots.tsx`), usato dalla lista degli slot e dalla testa: una **seconda
    schermata** che volesse un tooltip che si apre al tocco porta una decisione a Carmine, mai una copia (la risposta sulla #228, ripetuta
    sulla #249).
  - **Un fatto nuovo nella testa** di un evento (per esempio il luogo di un evento in presenza, M4c) si aggiunge come una voce in più di
    `facts` in `EventHead`, che gli mette davanti il suo puntino.
  - **E9** (fuori dal repository): M4a è nel codice; ⚠️ **la regola di Carmine sulla #233 cade con l'unione della #249** (l'eraser c'è), e
    **il log binario** resta da provare alla prima prenotazione sull'installazione di prova, alla consegna (risposta 2 sulla #233).
  - **E11a, E14b, E15b** (M4b) ed **E16, E17** (M4c): **«Duplica» si allarga** con le righe della loro fase — le postazioni ATC con le
    finestre, le regole di award, le domande e le attività —: una funzione in `EventCopy` per la riga, la lettura in `PlanAsync`, l'area in
    `AreasWritten`, l'aggiunta in `SaveAsync`. **Niente dei membri** (disponibilità, turni, PIREP, iscrizioni) si copia mai.
    **`EventsPersonalData` si allarga** con le loro righe (E15b per M4b, E16 ed E17 per M4c), con la stessa regola di «finito».
  - **E15b** (la conservazione, `events-retention`): `pilotRetentionMonths` conta da `EndsAtUtc`, e «finito» è `EventState.IsOver`.
  - ⚠️ **`EventsDuplicationTests.TheCopyNamesEveryColumnOfTheEvent`**: una colonna nuova di `evt_events` (nessuna fase dovrebbe
    migrarla: nasce intera in E2) fa cadere il test finché non si decide in `EventCopy.Of` se la copia la prende, la sposta o la lascia.
  - ⚠️ **I VID di prova degli eventi sono finiti**: con 761015 e 761098 di E8b, ogni VID di `761001–761099` è nel codice di un test. Una
    fase dopo riusa le persone di una classe unita (come E8b con 761063) o chiede a `dalberone` un'altra fascia.
  - ⚠️ **Nessun worktree, e nemmeno la cartella principale, ha `tiles/basemap.pmtiles`**: il giro completo gira senza (le mappe disegnano
    il fondo vuoto), e la riga di «Regole di tutte le fasi» sull'hard link vale solo per chi vuole vedere la mappa.
  - ⚠️ Un campo `datetime` del form generato è in UTC (`SchemaForm` scrive `${written}:00Z`): la spec scrive gli orari di «Duplica» così.

### Che cosa ha lasciato E10n (11–12 ottobre 2026, branch `m4/e10n-erased-audit-rows`, PR #251, del nucleo, in coda dopo la #252)

- **Che cosa c'è** (nota `decisions/2026-10-11-i-dati-di-una-persona-nell-audit.md`, **decisa** da Carmine sulla #249, [la risposta
  2][a249n2]: il buco dell'audit del [punto 7 del revisore][rv249n2], come meccanismo del nucleo per ogni modulo; la forma è della nota):
  - **Il segno** `[ErasedWith(nameof(…Vid))]` (`src/IvaoHub.Core/Privacy/ErasedWithAttribute.cs`) su una colonna di un'entità
    `[Audited]`: la colonna è un dato della persona che l'altra colonna, della stessa riga, nomina. `PersonColumns.ErasedWith(entity)` lo
    legge dal modello e rifiuta un segno che non nomina una colonna di persona (`Vid`, `…Vid`, `…By`).
  - **Il passo della cancellazione** `AuditRedaction.EmptyGoneAsync`, in ogni contesto dopo l'eraser e prima che l'audit prenda lo
    pseudonimo: le righe d'audit delle righe che nominavano la persona e **non ci sono più** — nessuna riga con quella chiave — perdono le
    colonne segnate (diventano `null` nel JSON) nelle copie scritte **mentre la riga era sua** (`EmptyWhileHeld`, dopo la revisione); il
    resto della storia resta, con lo pseudonimo. Una riga che c'è resta del suo eraser, di chiunque sia ora, come prima.
  - **Un segno sbagliato ferma l'avvio** (`PersonColumns.VerifyErasedWith` in `HubPipeline.InitializeAsync`, dopo la revisione): non una
    persona, un'entità non `[Audited]`, una proprietà non mappata, una chiave di più colonne.
  - **I test**: `ErasedAuditTests` (integrazione, 6 dopo la revisione) ed `ErasedColumnsTests` (unità, 10). Il modulo di prova segna `SampleItem.Title`
    come dato del membro di cui parla (`SampleItems.cs`, nessuna migrazione). **VID 761100** preso da questa fase (la persona cancellata,
    fuori dal blocco di M4, che è finito).
- **Che cosa devono sapere le fasi dopo**:
  - ⚠️ **E8b (la #249) segna le prenotazioni** dopo l'unione di questa (scelta di dalberone, l'11 ottobre: la regola 6 vuole il nucleo e
    il modulo in due PR): unisce `main` e mette `[ErasedWith(nameof(BookerVid))]` su `Callsign`, `OtherIcao`, `OtherTimeUtc`,
    `UnflownExcusedNote`, `FlownSessionId` di `EventBooking` — le cinque colonne che il suo eraser svuota —, con un suo test di una
    prenotazione ritirata prima della cancellazione (le copie senza nominativo, con lo pseudonimo). **Le prenotazioni non si aprono su
    un'installazione vera prima di E8b e di questa** (Carmine, risposta 2 sulla #249).
  - **Ogni colonna nuova che è un dato di una persona**, su una riga che si può togliere prima di una cancellazione (un ritiro, una riga
    tolta dallo staff), porta il segno con la colonna di persona della stessa riga. `ErasedAuditTests.EveryMarkOfThePersonsDataNamesAPersonColumn`
    legge i contesti di ogni modulo e cade su un segno sbagliato.
  - ⚠️ **Il limite**: un dato su una riga figlia, la cui persona è nominata solo dalla riga sopra (le note di una sessione di training),
    non è coperto. Se servisse, è un'altra nota.
  - ⚠️ **Le chiavi del JSON dell'audit sono i nomi delle proprietà** (`"Callsign"`), non in camel case: `Serialize` scrive un
    dizionario, le cui chiavi le opzioni web non cambiano. Il riassunto di `PersonColumns.IsVidName` diceva «camel case», ora corretto;
    un test che legge l'audit cerca `"Title"`.

[rv249n2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113085816
[a249n2]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/249#issuecomment-6113461471

### Che cosa ha lasciato E10m (11–12 ottobre 2026, branch `m4/e10m-names-reader`, PR #252, del nucleo; in coda dopo la #250, con la testa di E10l `2e4164d`)

- **Che cosa c'è** (nota `decisions/2026-10-11-un-solo-lettore-dei-nomi.md`, **decisa** da Carmine sull'issue #247, [la sua
  risposta][a247m2]; il dettaglio in `10`, E10m, «Com'è andata»):
  - **`MemberNames.ReadAsync`** (`src/IvaoHub.Core/People/MemberNames.cs`, namespace `IvaoHub.Core.People`), statico, con il
    `HubDbContext` di chi chiede: `ReadAsync(hub, IEnumerable<int> vids, ct)` e `ReadAsync(hub, IEnumerable<int?> vids, ct)` →
    `IReadOnlyDictionary<int, string>`. Il nome è `$"{FirstName} {LastName}".Trim()` in .NET; chi l'hub non conosce manca; un VID nullo
    manca; **un VID sotto uno non ha mai un nome** (una persona cancellata); **un nome vuoto resta `""`**; nessuno da chiedere non va al
    database. Nessuna registrazione, nessuna migrazione.
  - **I fili dei contatti lo usano** (`ContactThreads.ReadAsync`); la loro copia se n'è andata.
  - **`tests/IvaoHub.IntegrationTests/MemberNamesTests.cs`** (7): il lettore da solo, il test gemello con le tre copie dei moduli, e una
    prova per ogni differenza fra le quattro copie (nota §2). Usa le persone dei fili dei contatti (670011–670014) e rimette i loro nomi.
- **Che cosa devono sapere le fasi dopo**:
  - **La fase della formazione** (piccola, di dalberone, dopo l'unione di questa PR): `TrainingPeople.NamesAsync`
    (`src/IvaoHub.Modules.Training/TrainingPeople.cs`) risponde `MemberNames.ReadAsync(hub, vids, cancellationToken)`, oppure se ne va e
    i chiamanti chiedono al nucleo; `Member`, `Label` e `IsErased` restano. In `MemberNamesTests` esce la riga della formazione dal
    test gemello e da `AVidBelowOneIsNeverNamedEvenWhenAUserRowHasIt` (dopo il passaggio, la formazione non nomina più una riga sotto
    uno: è uno stato che l'hub non scrive, e la PR lo dice). Nessuna pagina cambia.
  - **La fase degli eventi** (piccola, di dalberone, dopo l'unione): lo stesso con `EventsPeople.NamesAsync`
    (`src/IvaoHub.Modules.Events/EventsPeople.cs`); `EventMemberDto` e `Member` restano; in `MemberNamesTests` esce la riga degli eventi.
    Le due fasi sono indipendenti e possono andare in parallelo: toccano righe diverse dello stesso test, che si uniscono senza conflitti
    se ognuna toglie solo la sua.
  - **I tour** (`PirepReview.NamesAsync`) li passa **una sessione di Carmine**; fino ad allora il test gemello tiene le due risposte
    uguali, e la riga dei tour in `MemberNamesTests` se ne va con lei.
  - **Ogni fase nuova che nomina qualcuno per VID** chiede a `MemberNames`, mai una copia; la pagina lo scrive con `personName` (o
    `col.person`) del browser, che fa del nome vuoto e di quello che manca il VID da solo.
  - ⚠️ **Il nome vuoto è `""`, non `null`**: chi vuole «nessun nome» per un nome vuoto lo dice nel suo `Member`, come i tour. Il lettore
    non lo decide per tutti, perché la mail della formazione e il `personName` lo leggono già a modo loro.
  - ⚠️ **Il nome si taglia in .NET, dopo la lettura**: unirlo nella query (`FirstName + " " + LastName`, `.Trim()` tradotto in `TRIM`)
    lascia un tab o uno spazio non separabile ai bordi. `ABlankOtherThanASpaceAtTheEdgeOfANameIsTrimmedToo` lo prova.
  - ⚠️ La sequenza di escape di U+00A0 scritta con gli strumenti Write ed Edit diventa il carattere: nel test è rimessa come
    sequenza con node.

[a247m2]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/247#issuecomment-6113460844

### Che cosa ha lasciato E10l (11–12 ottobre 2026, branch `m4/e10l-dialog-close-label`, PR #250, del nucleo; in coda dopo la #242, con la testa di E4c `8683d53` unita, `main` ad `a75ed6c`, con E7)

- **Che cosa c'è** (nota `decisions/2026-10-11-il-pulsante-di-chiusura-delle-finestre.md`, **decisa** da Carmine sull'issue #224,
  [la sua risposta][a224l]: comporlo nel nucleo, come la #238 per la paginazione; non una richiesta ad Atmosphere; il dettaglio in `10`,
  E10l, «Com'è andata»). Tutto nel browser, niente in C#, nessuna dipendenza nuova:
  - **`Dialog` del nucleo** (`web/src/shared/ui/Dialog.tsx`, da `shared/ui`), la **venticinquesima voce** dell'elenco chiuso
    (`catalog.ts`, galleria, `docs/UI-GUIDELINES.md` §3): la molecola `Dialog` di Atmosphere ricomposta dai suoi pezzi, con le stesse
    props (`trigger`, `title`, `description`, `children`, `footer`, `open`, `onOpenChange`…) e lo stesso aspetto; la «X» è un
    `DialogClose` nostro con `aria-label={t('common.close')}` — «Chiudi», «Close». **`closeButton`** (predefinito `true`) al posto di
    `topRightCloseButton`.
  - **La chiave `common.close`** nelle due lingue (e due della galleria).
  - **La guardia** (`web/src/shared/ui/Dialog.test.tsx`): nessun file di `web/src` importa da Atmosphere `Dialog`,
    `DialogTopRightClose`, `Sheet` o `SheetContent`, tranne quelli di `NOT_MOVED_YET`.
- **Che cosa devono sapere le fasi dopo**:
  - **La fase degli eventi dopo questa** (regola 6): `EventSlots.tsx` (il dialog di uno slot) e `PrivateSlots.tsx` di E7 (il dialog
    dell'ora privata) importano `Dialog` da `../../../shared/ui` invece che da Atmosphere — le props sono le stesse, e
    nessuno dei due passa `topRightCloseButton` — e i due file escono da `NOT_MOVED_YET` in `Dialog.test.tsx` (un test di dalberone).
    Nessun test né spec degli eventi cerca la parola «Close».
  - **Un dialog nuovo** si disegna con il `Dialog` del nucleo, una domanda con `ConfirmDialog`; mai il `Dialog` o il `Sheet` di Atmosphere:
    la guardia cade.
  - ⚠️ **La «X» non è in `DialogContent`**: in Atmosphere 3.1.0 la disegna `DialogTopRightClose`, montato dalla molecola `Dialog` e da
    `SheetContent`. Un `DialogContent` composto a mano (la palette di ricerca) non ha la «X» e non va spostato.
  - ⚠️ **`PrivateSlots.tsx` era in `NOT_MOVED_YET` prima di essere in `main`**, perché né E7 (#246) né questa PR facessero cadere
    l'altra; E7 è entrata per prima, e con la sua testa unita qui la guardia lo trova e lo ammette. Per lo stesso motivo la guardia
    chiede «nessuno fuori dalla lista», non «la lista uguale».
  - Il `Dialog` del nucleo cambia solo la «X»: **i campi senza bordo nel tema scuro** e **il contenuto lungo che scorre** che E7 ha trovato
    nei suoi dialog («Che cosa ha lasciato E7», sotto) restano com'erano.
  - ⚠️ **I toast dicono ancora «Close»**: `ToastClose` di Atmosphere, montato senza props dalla molecola `Toast` che `useNotice()` usa,
    ha «Close» come `aria-label` predefinito. Non è un dialog, la risposta di Carmine non lo copre: una domanda per Carmine, se dalberone
    la porta.
  - **I tour** non hanno dialog oggi; se ne avranno, sono del maintainer, e la guardia lo dice.

[a224l]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6113461064

### Che cosa ha lasciato E4c (9–12 ottobre 2026, branch `m4/e4c-events-titles-and-empty-lists`, PR #242, in fondo alla coda #242 → #250 → #252 → #251 → #249 → #239; `main` unito ad `a75ed6c`, con E7)

- **Che cosa c'è** (il dettaglio in `10`, E4c, «Com'è andata»; nessun file del nucleo, nessuna nota nuova): gli eventi usano i pezzi di
  E10k.
  - **`/events` e `/events/mine` dicono il loro titolo nella scheda**: `PageMetadata` con `events:public.title` ed
    `events:public.description` in `EventsPublicPage` (`web/src/modules/events/screens/public.tsx`), «Eventi — IVAO Italia»; con
    `events:mine.title` ed `events:mine.lead` in `MyBookingsPage` (`screens/mine.tsx`), «Le mie prenotazioni — IVAO Italia».
  - **La pagina di un evento** (`EventScreen`, `public.tsx`) non passa più `divisionName`: il nome viene dalla radice (`DivisionTitle`),
    e la scheda dice come prima il titolo dell'evento e «— IVAO Italia».
  - **Le cinque liste generate del modulo, vuote, dicono una frase loro** con `emptyDescription`, sotto il titolo del nucleo: le schede
    «Slot», «Rotte» (`events:routes.empty`), «Scali» (`events:airports.empty`) e «Prenotazioni» (`events:bookings.empty`) di un evento, e
    `/staff/events` (`events:events.empty`). La scheda «Slot» sceglie la sua dai tipi di slot dell'evento (`emptySlotsKey` in
    `screens/slots.tsx`): `events:slots.empty` con i soli pubblici, `events:slots.emptyPrivate` con i soli privati di E7,
    `events:slots.emptyBoth` con tutti e due. Parole che descrivono e non invitano (scelte di dalberone): vere per chi scrive, che ha i
    bottoni sopra, e per chi legge e basta, che non li ha.
  - **I test**: `web/src/modules/events/screens/titlesAndEmptyLists.test.tsx` (12, sulla radice vera dell'app) e due `toHaveTitle` nella
    smoke `web/e2e/events-public.spec.ts`.
  - **Le righe lasciate alla seconda** le ha fatte E4c: all'unione di E6b (10 ottobre) `bookingOpensAtUtc: null` nel `PublicEventDto` del
    test e il titolo di `/events/mine`; all'unione di E7 (12 ottobre) `privateSlots: []` nello stesso `PublicEventDto`.
- **Che cosa devono sapere le fasi dopo**:
  - **Una pagina nuova degli eventi** dice il suo titolo con `PageMetadata`, la stessa chiave del suo `H1`, senza `divisionName`; **una
    lista nuova del modulo** dice la sua frase vuota con `emptyDescription`, una chiave `<lista>.empty` passata dalla schermata (`DataList`
    non la legge da sé). Il test che lo prova sulla radice vera è `titlesAndEmptyLists.test.tsx`: una pagina o una lista nuova ci
    aggiunge la sua rotta e il suo caso.
  - ⚠️ **Il `PublicEventDto` del test è scritto per intero**: un campo nuovo e obbligatorio della lettura della pagina (come
    `bookingOpensAtUtc` di E6b e `privateSlots` di E7) va aggiunto lì, o il typecheck cade.
  - **Un modo nuovo di far nascere gli slot** (un'altra strada oltre a incollare, creare e generare) rilegge le tre frasi della scheda
    «Slot».
  - **La CI**: al primo push di questa fase «Test .NET» era rosso per #236, e la CI saltava i passi web (lint, `format:check`,
    typecheck, vitest, i18n, la smoke, il giro), fatti in locale ed elencati nella PR (`format:check` compreso: lo aveva mancato E10k,
    corretto da lei prima del push di questa). La #241 di Carmine l'ha corretto; con E10k unita a `fdb551d`, che porta `main` a
    `aa3707a`, la CI della #242 gira intera.

### Che cosa ha lasciato E7 (10–11 ottobre 2026, branch `m4/e7-private-slots`, PR #246; nata in coda dopo la #240, unita prima che la PR si aprisse; `main` unito a `0c2f85a`, con E10i, e dopo la revisione a `bea8c0e`, con E10k)

- **Che cosa c'è** (il dettaglio in `10`, E7, «Com'è andata» e «Dopo la revisione»; **nessuna migrazione**, niente del nucleo — solo
  `schema.d.ts` generato —; due note nuove, **tutte e due decise**: `2026-10-10-il-ritiro-di-un-volo-collegato`, da Carmine sull'issue
  #245 — la strada A: il legame si scioglie, l'altra prenotazione resta —, e `2026-10-11-gli-slot-privati`, da Carmine sulla #246 — sì
  alle letture (a)–(g); l'interruttore dei privati non passa da acceso a spento finché l'evento ne ha —; dopo la revisione, le sei
  correzioni del revisore sulla stessa PR):
  - **«Genera gli slot privati»**: il generatore, puro (`Staff/PrivateSlotGenerator.cs`), e il servizio (`Staff/PrivateSlotGeneration.cs`),
    `POST /api/events/events/{id}/slots/generate` (`EventBookings.Edit`), il bottone nella scheda «Slot». Dalla capacità di ogni scalo, ora
    per ora dell'evento, meno i voli che restano (i pubblici e i privati prenotati, in ogni scalo che toccano); passi regolari, lontano
    dagli orari tenuti; rigenerare sostituisce i liberi e tiene i prenotati; al più 5.000, **contati prima di costruire uno slot**
    (`PrivateSlotGenerator.Count`). **Una generazione per evento alla volta**: la riga dell'evento `FOR UPDATE` in una transazione
    `READ COMMITTED` intorno a tutto, l'evento riletto sotto il blocco; un deadlock è un 409 «leggi di nuovo».
  - **L'interruttore dei privati** (`EventSaving`): il passaggio da acceso a spento è rifiutato finché l'evento ha dei privati
    (`events:errors.hasPrivateSlots`, sul campo `privateSlots`); un evento che non l'ha mai avuto acceso non è chiesto.
  - **Prenotare un privato** con il suo volo, e un arrivo con la partenza collegata dallo stesso scalo, tutte e due o nessuna
    (`PilotBookings.BookPrivateAsync`, `POST /api/events/mine/bookings/private`). **Il volo di una prenotazione si legge solo da
    `BookedFlight`** (`Bookings/BookingRules.cs`): di un privato l'altro aeroporto e il suo orario li scrive il pilota, e l'off block di
    un arrivo è l'orario in cui lascia l'altro aeroporto — mai l'orario dello slot.
  - **Il legame** (`Bookings/BookingPairs.cs`): `paired_booking_id` sta sull'arrivo e nomina la partenza; `Of` dà le coppie nei due sensi
    quando ci sono tutte e due; `LetGoAsync` scioglie il legame nello stesso salvataggio in cui la partenza se ne va (il ritiro del
    pilota, «togli» dello staff).
  - **L'esportazione** porta i privati con il volo del pilota, il gate vuoto e `paired_slot_id` (aggiunta alla versione 1,
    `docs/events-bookings-export.md`); **la pagina** la sezione «Slot privati» (`screens/PrivateSlots.tsx`, `screens/privateList.ts`);
    `/events/mine` il volo collegato.
  - **I test**: unità `EventsPrivateSlotsTests` (17); integrazione `EventsPrivateSlotsTests` (14, sette dopo la revisione: le gare con
    una transazione del test tenuta nel database, il tetto, l'interruttore; VID **761087, 761088, 761095, 761096**; scali `XEI1`–`XEI3`,
    tipi `XE8A`/`XE8B`, slug `evt-test-e7-…`, voli `XEI8xx`/`XEI9xx` dopo la revisione); vitest `privateList.test.ts` e
    `PrivateSlots.test.tsx` (13); il giro completo `full/events-private.spec.ts` (slug `evt-test-e2e-e7-…`, voli `XEP801`/`XEP802`).
- **Che cosa deve sapere la fase dopo**:
  - **E8b** (la cancellazione, «Duplica»): un privato ha il volo scritto dal pilota sulla prenotazione (`callsign`, `other_icao`,
    `other_time_utc`): l'eraser degli eventi lo cancella con la riga. Una fase che toglie una sola prenotazione di una coppia chiama
    `BookingPairs.LetGoAsync` prima del `Remove`, come il ritiro e «togli». Gli slot privati hanno `Generated` vero: «Duplica» decide se
    copiarli o lasciarli al generatore, che si preme di nuovo sull'evento nuovo. ⚠️ **`PrivateSlotGeneration.GenerateAsync` apre la sua
    transazione** (e prende la riga dell'evento `FOR UPDATE`): non si chiama dentro un'altra — «Duplica», «una transazione», o lascia
    «Genera» allo staff o la chiama dopo il suo commit.
  - **E13a** (i voli fatti): il volo da cercare nel tracker è quello di `BookedFlight` — partenza, arrivo e orari di un privato li ha
    scritti il pilota —, non quello dello slot.
  - **E4c** (#242): `PublicEventDto` ha `privateSlots`, obbligatorio nel client generato: chi delle due, E4c o E7, arriva in `main` per
    seconda aggiunge `privateSlots: []` dopo `slots` nel `PublicEventDto` scritto per intero da `titlesAndEmptyLists.test.tsx` (con
    `bookingOpensAtUtc: null`, che E6b ha già chiesto).
  - ⚠️ **Due campi nuovi in fixture di fasi prima**, nessuna asserzione toccata: `pairedBookingId: null` nel campione della galleria
    (`blocks/index.ts`) e in `blocks/myEvents.test.tsx` di E6b, `privateSlots: []` in `event()` dello smoke `web/e2e/events-public.spec.ts`.
    Una fase che aggiunge un campo obbligatorio a `MyBookingDto` o a `PublicEventDto` fa lo stesso.
  - ⚠️ **L'interruttore dei privati non passa da acceso a spento finché l'evento ne ha** (la risposta 2 di Carmine sulla #246): solo
    quel passaggio, letto dal tracker, così `EventsSlotsTests` di E5, che scrive un privato su un evento che non l'ha mai acceso, resta
    com'è. La regola legge i privati quando l'evento si salva: una generazione che si chiude nello stesso istante può lasciarne su un
    evento spento — «elimina i liberi» li toglie.
  - ⚠️ **La lettura della pagina porta ogni slot privato a ogni lettore** (circa 0,4 MB al tetto, il punto 6 del revisore): detto nella
    nota e lasciato così. Una fase che la cambia (solo i conti, e una lettura dell'ora aperta) è una lettura scritta a mano in più.
  - ⚠️ **Nel tema scuro i campi di un dialog non hanno bordo** (i token di Atmosphere: `--input` e `--background` sono tutti e due
    `fuselage-900`): vale per ogni `Dialog` con un campo — il dialog dei privati, «Togli» di E6b, … —. È del nucleo; non toccato.
  - ⚠️ **Il dialog di un'ora con la partenza** è alto due voli di campi: il suo contenuto scorre sotto il titolo, al più il 65% della
    finestra. Un dialog nuovo con molti campi fa lo stesso, o a 1280×720 il bottone esce dalla finestra.
  - **E10i** (#237, unita qui): con un grant su un evento solo «Genera» crea e toglie gli slot di quell'evento come gli altri verbi della
    scheda — non provato da un test di E7.

### Che cosa ha lasciato E10k (9 ottobre 2026, branch `m4/e10k-lists-and-titles`, PR #238, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-09-le-parole-delle-liste-e-i-titoli-delle-schede.md`, **decisa** da Carmine sull'issue
  #224, [la sua risposta][a224k]: sì ai quattro punti; e sulla #238, [la sua risposta][a238k]: sì alla lettura più larga — il titolo
  predefinito su ogni pagina, il back office compreso, e i titoli di `/news`, `/documents`, `/search` — e la dipendenza da React 19
  accettata; il dettaglio in `10`, E10k, «Com'è andata» e «Dopo la revisione»). Tutto nel browser, niente in C#:
  - **La paginazione della lista generata** (`Pages` in `web/src/shared/list/DataList.tsx`): i pezzi di `Pagination` di Atmosphere
    (`PaginationRoot`, `PaginationContent`, `PaginationItem`, `PaginationLink`) con le parole `list.pages.*` del nucleo — «‹ Precedente
    1 2 3 Successiva ›», i due «…» per la prima e l'ultima pagina con il loro nome. Un «…» solo dove nasconde una pagina.
  - **La frase vuota per lista**: `emptyDescription` di `DataList`, la frase della schermata già tradotta; senza, `list.empty.description`
    come prima. Detta solo quando non si cerca niente.
  - **Il titolo della scheda**: `DivisionTitle` (`web/src/shared/seo/PageMetadata.tsx`), montato una volta da `Root` in
    `web/src/routes/__root.tsx`, scrive il nome della divisione come titolo predefinito di ogni pagina e lo dà all'albero sotto.
    `PageMetadata` prende anche una frase già tradotta come `title` e `description`, e `divisionName` è **facoltativo** (senza, il nome
    della radice). `NotFound` dice il suo titolo; `/news`, `/documents`, `/calendar` e `/search` il loro; `_public/$.tsx`, `HomePage` e
    `PublicEntryScreen` non passano più il nome a mano.
  - **La frase di Invio del form generato** (`whereEnterSaves` in `web/src/shared/forms/SchemaForm.tsx`): niente frase dove nessuna
    casella è di una riga, `form.submitHintOneLine` dove ci sono caselle di una riga e di più righe, `form.submitHint` com'era dove sono
    tutte di una riga. Le caselle di una riga di una **lista ripetibile non contano** (sullo schermo ci sono solo dopo «Aggiungi»), quelle
    di più righe sì (dalla revisione della #238).
  - **Sei chiavi nuove del nucleo** in `locales/{en,it}/common.json`: `list.pages.label`, `.previous`, `.next`, `.first`, `.last`,
    `form.submitHintOneLine`. Nessun doppione nei file dei moduli.
  - **I test** (vitest, 16): `web/src/shared/list/DataList.words.test.tsx`, `web/src/shared/forms/SchemaForm.enter.test.tsx`,
    `web/src/routes/-titles.test.tsx` (sulla radice vera). `docs/UI-GUIDELINES.md` dice tutto questo in inglese.
- **Che cosa devono sapere le fasi dopo**:
  - **La fase degli eventi dopo questa** (scelta di dalberone il 9 ottobre: la regola 6 vuole il nucleo e il modulo in due PR): in
    `EventsPublicPage` (`web/src/modules/events/screens/public.tsx`)
    `<PageMetadata title={t('events:public.title')} description={t('events:public.description')} />`; in `EventScreen`, lo stesso file,
    il `divisionName` calcolato a mano si toglie; le schede «Slot» (`screens/slots.tsx`) e «Rotte» (`screens/routes.tsx`) passano
    `emptyDescription={t('events:…')}` con parole loro, chiavi nuove in `events.json` (poi `pnpm i18n:sync`).
  - **Ogni pagina nuova** dice il suo titolo con `PageMetadata`, la stessa chiave del suo `H1`, e **non passa `divisionName`**: lo dice la
    radice. Una lista di un modulo dice la sua frase vuota con `emptyDescription`.
  - ⚠️ **Il titolo predefinito regge sull'ordine di React**: React 19 mette un `<title>` che monta prima di quelli già nella testa, e il
    browser mostra il primo; la radice monta prima di ogni pagina. **Mai un `<title>` predefinito sotto una pagina o in un layout**: il
    titolo di una pagina montata prima di lui perderebbe la scheda. `web/src/routes/-titles.test.tsx` lo prova sulla radice vera.
  - ⚠️ **`/tours` è del maintainer**: dice il nome della divisione (il predefinito) finché il maintainer non aggiunge la sua riga in
    `PublicToursPage`. La pagina di un tour passa ancora `divisionName`, e va bene così.
  - ⚠️ **`WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`** (unità, del maintainer) **cade dalle 06:00 UTC del 9 ottobre su ogni
    branch**, `main` compreso: chiede la storia del 9 settembre 2026, e oltre i 30 giorni di `IWeatherSource.HistoryWindow`
    `NoaaWeatherClient.GetHistoryAsync` risponde `null`. Non si tocca (regola 3): l'issue #236 lo porta a Carmine. Fino alla sua
    correzione `build-test` è rosso su ogni PR, e la PR lo dice. ⚠️ **E con «Test .NET» rosso la CI salta ogni passo dopo**: lint,
    `format:check`, typecheck, vitest, la smoke, il giro completo. Si fanno in locale, `pnpm -C web run format:check` compreso (E10k l'ha
    mancato al primo push), e la PR li elenca.
  - **In un test che naviga su un router fatto per il test**, `router.navigate({ to })` prende i tipi delle rotte registrate dall'app
    (`/search` vuole i suoi parametri): `router.history.push(path)` non ha tipi di rotta.

[a224k]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/224#issuecomment-6070089222
[a238k]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/238#issuecomment-6083876286

### Che cosa ha lasciato E10i (9–10 ottobre 2026, branch `m4/e10i-scoped-grant-writes`, PR #237, del nucleo, senza coda; `main` unito a `ba06d66`, con E6a ed E6b)

- **Che cosa c'è** (nota `decisions/2026-10-09-il-grant-su-una-riga-scrive-la-sua-riga.md`, **decisa** da Carmine sulla #235: sì alla
  fase, e un grant su un evento solo crea un figlio di quell'evento, mai un evento nuovo; e dopo la revisione sulla #237: l'alternativa
  che crea chiede lo scope della riga nuova, nessuna alternativa sposta una riga fra scope, «mai un evento nuovo» per convenzione, e
  `EventBookings.Edit` su un evento solo con la propria prenotazione resta com'è; il dettaglio in `10`, E10i, «Com'è andata» e «Dopo la
  revisione»):
  - **Il guardiano** (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`) chiede `{Area}.Edit` **con lo scope della riga**, come l'unico
    handler: in modifica (la riga com'è scritta), all'eliminazione (la riga com'era) e alla creazione (lo scope con cui la riga nuova
    risponde). Una domanda sola, `Holds`, per `Edit` e per ogni alternativa.
  - **Un grant su un evento solo** (`events:event:{id}`) cambia e toglie l'evento e le sue righe — scali, rotte, slot e prenotazioni —,
    ne **crea** i figli (uno slot, una rotta, un caricamento della tabella degli slot), e **non crea un evento nuovo**: lo scope proprio di
    un evento nuovo è `events:event:0`, la chiave che il database non ha ancora dato, che nessun grant nomina.
  - **Lo spostamento fra scope** (`IsMoved`): una modifica che cambia dipartimenti, FIR o scope chiede `Edit` anche sulla riga di prima,
    e nessuna alternativa la sposta.
  - **Il modulo di prova**: `SamplePart` (`smp_parts`, `tests/IvaoHub.IntegrationTests/SampleParts.cs`), una parte che risponde con lo
    scope del suo item (`SampleItem.ScopeOf`), migrazione `AddSampleParts`; i test `ResourceScopeWriteTests` (7, sul guardiano) ed
    `EventsScopedGrantTests` (2, sugli endpoint veri degli eventi), VID 761097.
  - Nessuna migrazione del nucleo, nessun endpoint, nessuna chiave, niente nel browser; l'unico handler, il motore CRUD e la lista generata
    non cambiano.
- **Che cosa devono sapere le fasi dopo**:
  - ⚠️ **Una riga figlia risponde con lo scope della riga sopra di lei** (`ResourceScope => Event.ScopeOf(EventId)`), com'è già per
    scali, rotte e slot: così un grant su un evento la scrive e la crea. Ogni tabella nuova delle righe dello staff di un evento (per
    esempio le postazioni di E11a, le regole di award di E14b) fa lo stesso, come vuole la nota `2026-09-29-chi-lavora-sugli-eventi`
    (punto 6), e il grant su un evento solo vale anche lì senza altro codice.
  - ⚠️ **Lo scope proprio di una riga si costruisce sulla sua chiave**, mai su qualcosa che chi la scrive sceglie (l'indirizzo, un codice):
    un grant scritto su quello creerebbe la riga. E **un grant con scope si scrive su una riga che esiste**: la schermata del modulo che
    darà il grant «su un evento solo» lo scrive per un evento che ha trovato (`ModuleGrants.GiveAsync`), mai su un id che le arriva e
    basta. Sono le due condizioni del riassunto di `IHasResourceScope`; «mai un evento nuovo» si regge su questa convenzione, accettata da
    Carmine senza un controllo sulla chiave (risposta 3 sulla #237).
  - ⚠️ **Lo scope di prima si legge dai valori originali del tracker** (all'eliminazione, e in modifica quando la riga si sposta): quindi
    **uno stub mai letto è creduto com'è scritto, scope compreso** — il limite di E10h vale anche qui, e l'endpoint carica la riga che
    scrive —, e **uno scope si costruisce dalle sole colonne mappate della riga** (`Event.ScopeOf(EventId)`), mai da una navigazione, che
    quella copia non carica.
  - **`EventBookings.Edit` tenuto su un evento solo** lascia correggere anche la propria prenotazione in quell'evento: Carmine lo lascia
    così (risposta 4 sulla #237), chi lo tiene è staff dell'evento e l'audit dice chi ha fatto che cosa.
  - **E6a ed E6b** (unite): con E10i un grant di `EventBookings.Edit` su un evento solo toglie le prenotazioni di quell'evento («Togli»
    della scheda «Prenotazioni» di E6b) e ne corregge gli slot: **la trappola «Un grant su un evento solo non scrive»** del blocco di E6a,
    qui sotto, **è chiusa da questa fase**. `EventsBookingsTests.TheStaffTakeABookingAwayWithThePermissionOnItsEventAsItIsNow` resta com'è
    (il membro di un evento solo agisce su un altro evento: 403 dall'handler); il caso positivo, se lo si vuole, è una riga in più di quel
    test, in una fase del modulo.
  - **`WeatherTests.AForecastIsAskedForWithADateAndWithoutHours`** (unità, del maintainer) cadeva dal 9 ottobre 2026 alle 06:00 UTC su ogni
    ramo: una data fissa contro `DateTime.UtcNow` e la finestra di 30 giorni di NOAA (l'issue #236, aperta dalla sessione di E6a). **La #241
    di Carmine l'ha corretta** (0.6.6): un ramo nato da `main` prima di `aa3707a` la prende unendo `main`, come ha fatto E10i.

### Che cosa ha lasciato E6b (9 ottobre 2026, branch `m4/e6b-booking-pages`, PR #240, in coda dopo la #233)

- **Che cosa c'è** (il dettaglio in `10`, E6b, «Com'è andata»; nessuna migrazione; del nucleo solo i due conteggi dei blocchi; una nota
  nuova, `2026-10-09-le-pagine-delle-prenotazioni`, **decisa** da Carmine sulla #240 — sì a tutto —; dopo la revisione, le quattro
  correzioni del dialog di uno slot: un 409 dice «riprova», chi accede dal dialog torna alla pagina, un rifiuto rilegge la pagina e il
  dialog segue lo slot com'è letto adesso, nessun colore libero):
  - **La pagina di un evento** (`screens/public.tsx`, `EventSlots.tsx`, `SlotBooking.tsx`, `BookingOpening.tsx`): i filtri degli slot
    nell'indirizzo (`direction`, `from`, `until`, `type`, `airline`, `rotation`: `eventPageSearchSchema`), offerti solo quando restringono;
    **«Prenota»** con la scelta dell'aereo e **«Prenota tutta la rotazione»** nel dialog di uno slot, solo quando il server lo
    accetterebbe, altrimenti il perché; **«Tuo»** sugli slot del lettore, dalla sua lista; la riga **«Prenotazioni»** con l'apertura e il
    conto alla rovescia (`bookingOpensAtUtc` è nella lettura della pagina, `PublicEventDto`); **la striscia di E4b** con gli scali
    dell'evento nei suoi giorni, nell'ora della divisione (`EventDayStrip`), dentro la pagina sotto il titolo.
  - **`/events/mine`** (`screens/mine.tsx`, una rotta del membro): «Da volare» e «Passate», per evento, «Ritira» finché `withdrawable`.
    L'indirizzo `mine` non lo prende nessun evento (`EventWriteDtoValidator.MinePage`, `events:errors.slugReserved`).
  - **Il blocco `events.myEvents`** (`blocks/myEvents.tsx`, `Bookings/MyEventsProvider.cs`, sempre vivo): le prenotazioni ancora da volare
    (`PilotBookings.MineAsync`), a un visitatore `signedIn: false`. Nessun seme lo mette su `/me`: lo mette il back office.
  - **La scheda «Prenotazioni»** dello staff (`screens/bookings.tsx`): `GET /api/events/bookings`, una risorsa `MapCrud` in sola lettura
    (`StaffBookings`: l'ordine dell'orario del volo, il pilota `{ vid, name }` da `EventsPeople`), `col.person`, «Togli» con il motivo.
  - **Il promemoria del giorno prima**: `events-reminders` (`Bookings/BookingRemindersJob.cs`), ai minuti 10, 25, 40, 55 UTC;
    `events.bookingReminder` una volta per prenotazione (`reminded_at`, `[NotAudited]`), le vicine in una mail con le rotte del FOD, sotto
    un `FOR UPDATE` in `READ COMMITTED`; scrive la sua riga in `hub_jobs_log` a ogni giro, come `events-release`.
  - **«Pubblica» chiede conferma** (`screens/events.tsx`).
  - **I test**: `EventsBookingPagesTests` (integrazione, 6, VID **761063–761066**, scali `XEG1`–`XEG3`, tipi `XE7A`/`XE7B`, slug
    `evt-test-e6b-…`), `EventsRemindersTests` (unità, 4), vitest, sei smoke in `events-public.spec.ts`, il giro completo
    `full/events-bookings.spec.ts` (slug `evt-test-e2e-e6b-…`, voli `XEE601`–`XEE603`) e la conferma in `full/events-staff.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - **E7** (i privati): il promemoria, il blocco e `/events/mine` contano sull'off block di uno slot pubblico — il job prende solo
    `SlotKind.Public` con un off block, e mostra quello che `MineAsync` dà —. Un privato ha l'orario allo scalo e `other_time_utc`: E7
    allarga la query dei dovuti e `RemindAsync` di `BookingRemindersJob`, le righe della mail (`EventsMail`, `Flight`) e decide quale
    orario fa «dovuto». Il dialog di `SlotBooking` è di uno slot pubblico; i filtri leggono la compagnia dal nominativo dello slot.
  - **E8b** (la cancellazione): la scheda dello staff dice lo pseudonimo con `col.person`; il blocco e `/events/mine` sono del pilota
    stesso; il promemoria non va a uno pseudonimo (`EventsMail`).
  - **E12** (il roster): `events.myEvents` aspetta i turni. La risposta ha `signedIn` e `bookings`; i turni sono un campo in più della
    stessa risposta, e il blocco li disegna sotto le prenotazioni.
  - **E13a/E13b**: `reminded_at` è la contabilità del job, non un dato del volo, e non lascia righe d'audit.
  - **E4c** (dopo la #238 di E10k, i titoli e le frasi delle liste vuote degli eventi): la scheda «Prenotazioni» è una lista generata in
    più degli eventi (`screens/bookings.tsx`, le parole `events:bookings`), e non c'era quando E4c è nata; e **`/events/mine` non dice il
    suo titolo** nella scheda del browser: con il `PageMetadata` di E10k è una riga, `<PageMetadata title={t('events:mine.title')} />`,
    come per `/events`. La aggiunge chi delle due, E4c o E6b, prende l'altra per seconda; e chi lo fa aggiunge anche `bookingOpensAtUtc:
    null` dopo `endsAtUtc` nel `PublicEventDto` scritto per intero da `titlesAndEmptyLists.test.tsx` di E4c (il campo di E6b è
    obbligatorio nel client generato; detto dalla sessione di E4c il 9 ottobre). Per il resto i due branch si uniscono da soli, tranne
    l'intestazione di questo file.
  - ⚠️ **Le schede dello staff di un evento sbordano a 1280 px** con la barra laterale: «Prenotazioni» (1026 px su 927) come «Slot» di
    E5 (1052): la lista scorre, e «Togli» si raggiunge scorrendo; a 1440 px ci stanno. Il dialog di uno slot dice «Close» in inglese al
    lettore di schermo (il `Dialog` di Atmosphere, del nucleo).
  - ⚠️ **Il banco del giro completo riacceso per guardare** manda subito la coda di mail che il giro ha lasciato: chi lo riaccende con
    Mailpit tiene il lock di Mailpit.
  - **E10j** (#239, il recupero dei giri): `events-reminders` decide dai suoi dati, quindi un giro recuperato o ripetuto non manda niente due
    volte; il suo trigger dice il fuso (UTC).
  - ⚠️ **E10i** (#237): finché non è unita, un grant su un evento solo mostra la scheda «Prenotazioni», ma «Togli» risponde 403 (il
    guardiano, «Che cosa ha lasciato E6a»).
  - ⚠️ **Un lettore di nomi per VID nel nucleo, prima di un quinto** (risposta 4 di Carmine sulla #240): oggi ce ne sono quattro —
    `PirepReview.NamesAsync` dei tour, `TrainingPeople`, `ContactThreads` del nucleo ed `EventsPeople` —; una fase che ne vorrebbe un
    altro aspetta quello del nucleo, che entra nella sua coda, e i quattro poi passano a lui. La striscia degli scali resta dentro la
    pagina (risposta 2): il banner sarebbe una fase del nucleo, non chiesta.
  - ⚠️ **Un 409 di una prenotazione non passa da `describeProblem`**: il nucleo risponde a ogni 409 con la sua frase («qualcuno ha
    cambiato…») e non legge il titolo del server; il dialog dice `events:errors.bookingTryAgain`. Lo stesso vale per i verbi dei privati
    di E7. E un link d'accesso porta il percorso e la query (`window.location.pathname` + `search`): `SafeReturnUrl` del server manda
    alla home un indirizzo intero — lo stesso difetto è in `main` nelle pagine di tour e training (il punto 9 della revisione, non di
    questa fase).
  - ⚠️ **Nessuna prenotazione su un'installazione vera finché E8b non è unita**, e il log binario si prova alla consegna (risposte 2 e 4 di
    Carmine sulla #233): vale anche per il segno del promemoria, scritto in una transazione `READ COMMITTED`.
  - ⚠️ **Una mail del giro completo** parte con la coda del nucleo, una volta al minuto, **nella lingua della persona**: il pilota del
    banco legge in italiano. Una spec che la aspetta alza `test.setTimeout` e la cerca per il timbro del giro, che sta nel titolo delle
    due lingue.
  - ⚠️ `englishCommon` (`web/e2e/locales.ts`, del nucleo) non ha `common.cancel` né `liveStatus.airportsTitle`: le spec del modulo le
    leggono da `locales/en/common.json`.
  - **`WeatherTests`** (#236) è corretto dalla #241 (0.6.6): la #240 porta `main` a `aa3707a` attraverso la testa di E6a, e la CI gira
    intera.

### Che cosa ha lasciato E6a (7–9 ottobre 2026, branch `m4/e6a-booking-server`, PR #233; la #228 e la #232 sono unite)

- **Che cosa c'è** (il dettaglio in `10`, E6a, «Com'è andata»; una migrazione additiva, `AddEventBookings`; del nucleo solo le due righe
  di `ErasureTests`; due note nuove: `2026-10-07-le-prenotazioni-sul-server`, **decisa** da Carmine sulla #233 l'8 ottobre — sì alle
  prime dieci letture, il log binario aperto fino alla consegna, uno slot prenotato corretto avvisa il pilota, l'eraser a E8b con la sua
  regola —, e
  `2026-10-07-le-colonne-delle-prenotazioni-in-erasuretests`, nessuna decisione nuova; il ritiro poggia sulla fase del nucleo **E10h**, la
  #232, unita a questo branch; dopo l'apertura della PR, la testa di E5 dopo la prova sul banco, `25d23f5`, unita anche lei — `d767cc0`,
  i conflitti dell'esportazione e del commento dello slot risolti tenendo i due lati —; **le correzioni della revisione del master**
  ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6039678626)), sulla stessa PR, punto per punto in `10`;
  poi, quando la coda si è mossa, la testa di E5 che ha preso E10g e `main` a `7b84a75` (`ecf88b9`, unita in `67c870f`), quella decisa di
  E10h (`ec9b3b4`, in `8ab7c69`) e l'ultima di E5 (`ba53a97`, in `d36f4b7`), conflitti solo nei documenti: la PR non è più in conflitto
  con `main`; l'8 ottobre la testa finale di E10h (`eb8e8ef`, in `6451158`), con `main` a `1f2a687`, e la frase che Carmine chiede a ogni
  fase che mette il segno — perché la prenotazione non porta una decisione, la nota §2):
  - **`evt_bookings` intera** (`EventBooking`, design §1.6): `event_id`, `slot_id` (univoco), `booker_vid`, `aircraft_icao`, `callsign`,
    `other_icao`, `other_time_utc`, `paired_booking_id` (E7), `flown_at`, `flown_session_id`, `flown_checked_at` (E13a),
    `unflown_excused_by`, `unflown_excused_note` (E13b), `reminded_at` (E6b), `created_at`, più dipartimento, maschera e visibilità
    (`Members`). Una riga di un membro: `ISubmittedByMembers`, `IHasStakeholder` (il pilota), `[Audited]`, lo scope dell'evento,
    `[WithdrawnByStakeholder]` (E10h), e un **`IEventChild`**: la cura dell'evento si copia quando lo staff agisce, non solo il giorno della
    prenotazione. Non `IAuditable`. **Una chiave verso lo slot con `RESTRICT`** (uno slot prenotato non si elimina, nemmeno nel
    database), **nessuna verso l'evento** (il blocco del pilota, sotto).
  - **I verbi del pilota** (`/api/events/mine/bookings`, `Bookings/`): `GET` le sue (anche passate, per off block), `POST` uno slot
    pubblico con l'aereo, `POST …/rotation` tutta la rotazione di una tratta con un aereo solo (sempre 200: le prenotate, e il perché di
    ogni altra), `DELETE …/{id}` il ritiro fino all'off block. **Il blocco del pilota**: `SELECT … FOR UPDATE` sulla sua prima
    prenotazione dell'evento, o sulla riga dell'evento, in una transazione **`READ COMMITTED`** (`PilotBookings.LockThePilotAsync`), e
    l'evento si rilegge sotto il blocco, con la condizione della prima lettura (`EventState.Seen`: un annullamento salvato mentre la
    prenotazione aspettava si vede, e un evento che ha smesso di vedersi è 404). I rifiuti su `slotId` (`slotAlreadyYours`,
    `slotJustTaken`, `bookingIncompatible`, `slotClosed`, `bookingNotOpen`, `bookingCancelledEvent`, `bookingPrivateSlot`) e su
    `aircraftIcao` (`aircraftNotAllowed`); 404 per un evento che il pilota non vede; **409 «riprova»** (`events:errors.bookingTryAgain`) per
    un deadlock, di una prenotazione come della rotazione — mai «preso»; 201 senza un indirizzo (`Location`).
  - **Lo staff toglie** (`POST /api/events/bookings/{id}/remove`, `EventBookings.Edit` sulla prenotazione nella cura dell'evento com'è
    ora — `EventChildren.AdoptAsync` prima del permesso —, un motivo obbligatorio) e il pilota riceve **`events.bookingRemoved`**, dopo che
    la cancellazione è salvata (voluto: la nota, lettura 6); **`EventsMail`** è il posto delle mail del modulo (un intento per lingua, il
    titolo `{{title}}`, mai a uno pseudonimo).
  - **Le regole che crescono**: `SlotRows.Free` = gli slot che nessuna prenotazione nomina; uno slot prenotato non si elimina
    (`slotBooked`); un evento con prenotazioni non si elimina (`eventHasBookings`); annullare manda `eventCancelled`, spostare l'inizio o la
    fine `eventChanged` (`EventSaving.AfterSaveAsync`), a chi ha prenotato, una volta per persona. **Uno slot prenotato si corregge
    ancora** dal suo form e la prenotazione resta; se la correzione cambia callsign, orari, aeroporti o tipi ammessi (questi come insieme),
    il pilota riceve **`events.bookingChanged`** con il volo com'è ora e il suo aereo (`SlotSaving.AfterSaveAsync`, risposta 3 di Carmine);
    stand, numero di volo, rotazione o il solo tipo principale non avvisano nessuno. La compatibilità non si ricontrolla.
  - **La pagina** dice «preso» (`PublicEventSlotDto.Taken`), **l'esportazione** `booked_by` e `aircraft_icao`, accanto ad
    `aircraft_types` di E5 (i tipi ammessi, il principale per primo).
  - **I test**: `EventsBookingsTests` (unità, 12; integrazione, 16, VID 761043–761046 e, dalla revisione, 761084–761086 — tre membri
    dello staff per un grant a un VID, senza indirizzo, avanzi di E10f e di E4b; liberi ancora 761087–761088 e 761096–761098 —, scali
    `XEF1`–`XEF4`, tipi `XE6A`/`XE6B`, slug `evt-test-e6a-…`), con **le gare
    deterministiche**: una transazione del test (`HeldTransaction`) tiene quello che terrebbe la prima richiesta, la richiesta in prova
    aspetta nel database (`INNODB_TRX`, con root), il test conferma; e, dalla revisione, un deadlock costruito (la transazione del test più
    pesante, così il database riporta indietro il pilota) e sei prime prenotazioni mandate insieme. VID 761047–761048 e 761037 sono di
    E10h.
- **Che cosa deve sapere la fase dopo**:
  - **E6b** (le pagine): i verbi ci sono tutti, e il client generato li conosce (`schema.d.ts`). Manca **la lista dello staff** delle
    prenotazioni: una risorsa `MapCrud` in sola lettura (`EventBookings.View`, `filter[eventId]`, `Source` con `CrudSource.BackOffice`) con
    il pilota come `{ vid, name }` — una pagina di nomi per pagina di righe (`ToListPage`), come `TrainingPeople` — per `col.person`, e
    «togli» chiama `…/remove`. La pagina di un evento sa già «preso»; «Prenota» — nel dialog dello slot di E5 (`SlotDetail`, «Che cosa
    ha lasciato E5») — manda `POST /api/events/mine/bookings` con lo slot e il tipo scelto fra quelli ammessi, e i rifiuti arrivano su
    `slotId` e `aircraftIcao`; «Prenota tutta la rotazione» mostra le tratte di `notBooked` con il loro perché (chiavi `events:errors.*`).
    Un **409** di tutti e due è «riprova» (il titolo del problema): niente è prenotato, e la stessa richiesta può passare un attimo dopo.
    **E4b è in questo branch** (con `main` a `7b84a75`, dalla testa di E5): E6b, che nasce da qui, trova la striscia «chi è online sugli
    scali» da montare («Che cosa ha lasciato E4b»). `/events/mine` legge `GET` e ritira con `DELETE`; `withdrawable` dice se si può. **Il promemoria** (`events-reminders`) scrive `reminded_at`: valuti `[NotAudited]` per quella colonna, come l'ultimo uso di un
    token (T19a), se il suo giro non deve riempire l'audit. `EventsMail` ha il posto per `bookingReminder`.
  - **E7** (i privati): `BookingRules.ClosesAt` e `BookingInterval.Of` rispondono solo per uno slot pubblico (un privato oggi è chiuso e
    senza intervallo); E7 li allarga con l'orario allo scalo e `other_time_utc`, e `PilotBookings.BookAsync` rifiuta un privato con
    `bookingPrivateSlot` finché E7 non porta il suo verbo. L'esportazione legge già la prenotazione per ogni slot (`Flight(slot, booking)`).
  - **E8b** (la cancellazione): oggi il nucleo scrive lo pseudonimo in `booker_vid` e lo slot resta preso da nessuno; `EventsPersonalData`
    cancella le prenotazioni degli eventi non conclusi (il ritiro di E10h non serve: è un job del nucleo, in modalità cancellazione) e
    svuota i dati del volo privato di quelle tenute. L'esportazione dice un `booked_by` negativo, e il documento lo spiega.
  - **E13a/E13b**: le colonne `flown_*` e `unflown_excused_*` ci sono; un job che le scrive è anonimo e il guardiano lo lascia.
    ⚠️ **La prenotazione porta `[WithdrawnByStakeholder]` perché non porta una decisione** finché il pilota può ritirarla (la regola di
    Carmine sulla #232, punto 6: la nota di E6a, §2, dice perché). La giustificazione di E13b e la verifica di E13a cadono dopo il volo. Ma
    uno slot può partire fino a sei ore dopo la fine dell'evento (`SlotWindow.Margin`) e `events-after` gira dopo `ends_at_utc`: non
    toccate una prenotazione ancora ritirabile, o il segno va riguardato.
  - ⚠️ **Il log binario, punto aperto** (risposta 2 di Carmine): con `binlog_format=STATEMENT` MariaDB rifiuta le scritture di una
    transazione `READ COMMITTED`, e ogni prenotazione cadrebbe. Il predefinito di MariaDB 11.4 è `MIXED`; la CI ha il log spento; il
    maintainer non vede le variabili del server. Si prova con la prima prenotazione sull'installazione di prova, alla prossima consegna,
    prima di aprire le prenotazioni di un evento vero: lo chiude il maintainer.
  - ⚠️ **Nessuna prenotazione su un'installazione vera finché E8b non è unita** (risposta 4 di Carmine): l'eraser degli eventi arriva con
    E8b; l'installazione di prova non è vincolata.
  - ⚠️ **Uno slot prenotato corretto non si ricontrolla**: la prenotazione resta anche se ora è troppo vicina a un'altra del pilota, o se il
    suo aereo non è più ammesso; la mail `bookingChanged` glielo fa sapere e decide lui (ritirarla fino all'off block). E6b mostra le
    prenotazioni del pilota: lì si può dire che una non va più con un'altra.
  - ⚠️ **Il motivo di «togli» non si conserva**: è nella mail; l'audit dice chi ha tolto che cosa e quando.
  - ⚠️ **Un grant su un evento solo non scrive** (trovato in E6a, del nucleo, non toccato qui; lo porta la fase del nucleo **E10i**, in
    corso, che fa chiedere al guardiano `{Area}.Edit` con lo scope della riga): passa l'unico handler, ma il guardiano
    dell'interceptor chiede `{Area}.Edit` sui dipartimenti della riga senza il suo scope (`RequireAny`), e il salvataggio lo rifiuta
    (`ForbiddenDomainException`) — una prenotazione da togliere come uno slot da correggere. Il design vuole un grant a un VID «anche su un
    evento solo» (§6): finché il nucleo non lo legge, la lista dello staff di E6b lo mostra a chi ha quel grant e «togli» risponde 403.
  - ⚠️ **Due 500 restano**: un'attesa di blocco scaduta in una prenotazione (50 secondi, `innodb_lock_wait_timeout`) e un deadlock in
    «elimina i liberi»; rari, la risposta giusta sarebbe «riprova».
  - ⚠️ **`EventsArchitectureTests` rifiuta la stringa `"event"`** anche come nome di un segnaposto di una mail: il titolo è `{{title}}`.

### Che cosa ha lasciato E10h (7 ottobre 2026, branch `m4/e10h-stakeholder-withdraws`, PR #232, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga.md`, **decisa** da Carmine sulla #232: sì alla
  forma, il limite dello stub scritto come per T11, e sul rilievo 6 una frase nel riassunto del segno, §6.3 della nota; il dettaglio in
  `10`, E10h, «Com'è andata» e «Dopo la revisione»):
  - **Il segno**: `[WithdrawnByStakeholder]` (`WithdrawnByStakeholderAttribute`, namespace **`IvaoHub.Core.Division`**, in
    `src/IvaoHub.Core/Division/DomainContracts.cs` accanto a `ISubmittedByMembers`), sulla classe dell'entità.
  - **Il guardiano** (`HubSaveChangesInterceptor.EnsureWriteIsAllowed`): una riga `ISubmittedByMembers` e `IHasStakeholder` la cui entità
    ha il segno la **cancella** il membro che ne è l'interessato, **com'era caricata** (i valori originali del tracker: chi legge la riga
    di un altro, ci scrive il suo VID e poi la toglie non passa), e nessun altro senza `{Area}.Edit` (o un'alternativa segnata
    `AlsoOnDeletion`). Senza il segno cancellarla resta del dipartimento (il PIREP, il training). Lo staff con `{Area}.Edit`, il
    superadmin e l'anonimo come prima; l'audit scrive `deleted` con il VID di chi ha cancellato.
  - **All'avvio** `HubSaveChangesInterceptor.VerifyWithdrawals`, chiamato da `HubPipeline.InitializeAsync` accanto a
    `VerifyAlternatives`, ferma l'hub se il segno sta su un'entità che non è insieme `IOwnedByDepartment`, `ISubmittedByMembers` e
    `IHasStakeholder`.
  - **Il modulo di prova**: `SampleSubmission` (`smp_submissions`, con il segno) e `SampleReport` (`smp_reports`, senza) in
    `tests/IvaoHub.IntegrationTests/SampleSubmissions.cs`, migrazione `AddSampleSubmissions`; i test `WithdrawnByStakeholderTests`
    (integrazione) e `VerifyWithdrawalsTests` (unità). `ErasureTests` non cambia: non legge il contesto di prova.
  - Nessuna migrazione del nucleo, nessun endpoint, nessuna chiave, niente nel browser; l'unico handler e il motore CRUD non cambiano.
- **Che cosa devono sapere le fasi dopo**:
  - **E6a** (la #233, in coda dopo la #232): il segno su `EventBooking` l'ha già messo la sua sessione, che ha unito questo branch; il
    ritiro passa dal suo endpoint del flusso del pilota (`DELETE /api/events/mine/bookings/{id}`), che legge la riga del pilota e la
    elimina, e il guardiano controlla di nuovo sotto.
  - ⚠️ **Il segno non apre il motore CRUD**: la DELETE di `MapCrud` chiede all'handler la policy di scrittura, e un membro non la tiene.
    Il ritiro di un membro è sempre un endpoint del suo flusso.
  - ⚠️ **L'endpoint deve caricare la riga; uno stub passa** (risposta 2 di Carmine sulla #232, come per T11): il guardiano legge
    l'interessato dai valori originali del tracker, e per una riga attaccata senza leggerla (`Remove(new X { Id = id, … })`) sono quelli
    che ha scritto chi chiama: un membro toglierebbe la riga di un altro. L'endpoint legge la riga del membro — per id **e** per
    interessato, come `PilotBookings.WithdrawAsync` di E6a — e poi la toglie. Lo fissa
    `WithdrawnByStakeholderTests.AStubNeverLoadedIsBelievedAsItsCallerWroteIt`.
  - **E11a** (la disponibilità di un controllore, che «la ritira fino alla chiusura», design §4.2) ed **E16** (l'iscrizione a un evento
    in presenza, «si ritira fino all'inizio dell'evento», §4-bis.2): se la fase dice che ritirare vuol dire cancellare la riga, basta il
    segno sull'entità, che dev'essere `IOwnedByDepartment`, `ISubmittedByMembers` e `IHasStakeholder` (o l'hub non parte); se il ritiro è
    uno stato, come per il PIREP, il segno non serve: cambiare la propria riga lo permette già l'eccezione di T11.
  - ⚠️ **Il segno si legge con `inherit: false`**: va sulla classe dell'entità, non su una sua base.
  - ⚠️ **Il segno mai su una riga su cui lo staff decide qualcosa del membro** (risposta 3 di Carmine sulla #232, nota §6.3: una frase
    nel riassunto del segno, non un rifiuto all'avvio): un PIREP di supporto, la cessione di un turno — cancellarla cancellerebbe la
    decisione. Il guardiano non sa né lo stato né l'ora: fino a quando si ritira lo dice l'endpoint. **La nota di ogni fase che mette il
    segno dice perché la sua riga non porta decisioni**, e la revisione lo controlla (per una prenotazione: le verifiche e le scuse dello
    staff arrivano dopo l'evento, quando il ritiro, che finisce all'off block, è chiuso).

### Che cosa ha lasciato E5 (6–7 ottobre 2026, branch `m4/e5-public-slots`, PR #228, nata in coda dopo la #223, unita prima che si aprisse; dalla revisione in coda dopo la #230 di E10g)

- **Che cosa c'è** (il dettaglio in `10`, E5, «Com'è andata»; una migrazione additiva, `AddEventSlots`; del nucleo solo le due righe di
  `ErasureTests`; due note nuove: `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, **decisa** da Carmine sulla #228 — [le sue
  risposte][a228]: sì alle otto letture, e i punti 9 e 10 sulle domande del revisore ([osservazioni][v228]) —, e
  `2026-10-06-le-colonne-degli-slot-in-erasuretests`, nessuna decisione nuova; il controllo della versione dell'esportazione è del nucleo,
  dalla #230 di E10g, unita a questo branch; dopo la prova sul banco, una terza nota, `2026-10-07-gli-slot-sulla-pagina-dell-evento`, il
  tipo principale e la pagina pubblica degli slot che dalberone ha chiesto, **decisa** da Carmine sulla #228 — [le sue risposte][a228b]:
  sì ai sette punti, e un ottavo sulla [seconda lettura del revisore][v228b], `Hint` resta un pezzo di questa schermata):
  - **`evt_slots` intera** (`EventSlot`, design §1.5), per i pubblici di E5 e i privati di E7: `kind`, `event_airport_icao`, `is_arrival`,
    `callsign`, `flight_number`, `aircraft_types` (JSON), `departure_icao`, `arrival_icao`, `off_block_utc`, `on_block_utc`, `stand`,
    `rotation_code`, `rotation_leg`, `generated`, `row_version`; univoco `(event_id, callsign, off_block_utc)` (un privato non ha callsign, e
    l'indice ne lascia passare quanti vuole). Riga `IEventChild` nell'area **`EventBookings`**, `[Audited]`, lo scope dell'evento.
  - **Incolla o carica** (`POST /api/events/events/{id}/slots/load`, `EventBookings.Edit` sull'evento; `Staff/SlotLoading.cs`): il testo —
    incollato da un foglio di calcolo, o il file CSV letto dal browser nella stessa casella — e il modo (`Add`, `ReplaceFree`). **Lo legge il
    server** (`Staff/SlotSheet.cs`): l'intestazione del design in qualunque ordine, tabulazioni, punto e virgola o virgole, le virgolette; le
    righe contate come le conta la tabella (`rows[12].aircraft_types`); al più mille. Ogni riga uno slot pubblico (`SlotDraft.Read`): orari
    solo `2026-10-17 14:30` in UTC, tipi separati da `/`; tipi e scali chiesti al nucleo una volta per tutta la tabella; **il verso dagli
    ICAO** (`SlotDirection`: partenza da uno scalo dell'evento, anche fra due scali dell'evento, altrimenti arrivo, altrimenti rifiutato);
    callsign e off block una volta nell'evento; **le rotazioni** (`SlotChains`, con gli slot salvati che restano): i posti, o gli orari
    quando nessun posto è scritto, lo scalo che coincide, l'ordine, `bookingGapMinutes`; **la finestra dell'evento** (`SlotWindow`, il
    punto 10 di Carmine): l'orario allo scalo dell'evento — l'off block di una partenza, l'on block di un arrivo — fra sei ore prima
    dell'inizio e sei ore dopo la fine, `events:errors.slotOutsideWindow` sulla colonna di quell'orario; l'altro orario è libero. **Tutto
    o niente**, una transazione; **409 «carica di nuovo» solo per un'altra scrittura delle stesse righe** (una chiave che l'indice univoco
    ha già, o il deadlock di due insert della stessa chiave, cercati nella catena dell'eccezione come fa `InitialisationMarker`): ogni
    altro errore esce com'è (revisione, punto 4).
  - **La lista e il form generati** (`/api/events/slots`, `MapCrud`, `EventBookings.View`/`.Edit`): la scheda **«Slot»** della pagina
    dell'evento, su un evento con slot e scali suoi; il form di uno slot (`/staff/events/{id}/slots/{slotId}`, anche «Nuovo slot») tiene le
    regole del caricamento (`SlotSaving`, la finestra compresa), e i tipi in **due campi, «Tipo principale» e «Altri tipi»**
    (`MainAircraftType`, `OtherAircraftTypes` di `EventSlotWriteDto`), salvati principale per primo (`SlotValues.MainFirst`), ogni rifiuto
    sul suo campo; **il primo di `aircraft_types` è il principale**, anche nella cella `A320/A20N` della tabella; **«Elimina i liberi»**
    (`POST …/slots/delete-free`): ogni slot libero dell'evento, pubblici e privati. La pagina del caricamento
    (`/staff/events/{id}/slots/load`): l'intestazione da copiare, il file CSV, i rifiuti elencati per riga e colonna.
  - **Le regole che crescono**: niente caricamento su un evento senza slot pubblici o senza scali; l'interruttore degli slot pubblici non si
    spegne sotto gli slot (`events:errors.hasPublicSlots`); uno scalo con slot non si elimina né cambia codice (`airportHasSlots`); eliminare
    un evento porta via i suoi slot con l'audit (`EventSaving.DeleteAsync`).
  - **La pagina dell'evento** elenca gli slot pubblici (`PublicEventDto.Slots`, nella lettura che c'è già; `screens/EventSlots.tsx` e
    `screens/slotList.ts`), come dalberone l'ha chiesta dopo il banco (nota del 7 ottobre, «Proposta»): **per scalo dell'evento**, una
    sezione ciascuno quando sono più d'uno, e in ognuno **«Partenze» e «Arrivi»** in due tabelle, per l'orario allo scalo; **il tipo
    principale**, gli altri nel tooltip di Atmosphere al passaggio del mouse, al focus e al tocco; le tratte di una rotazione ognuna nella
    sua tabella, **segnate da un'icona** (`Repeat`) che lo dice; **una riga apre lo slot** in sola lettura in un dialog di Atmosphere, con
    tutti i tipi ammessi, gli orari, lo stand e le tratte della rotazione — e quando si chiude **il focus torna al nominativo** che l'ha
    aperto; un click che chiude una selezione di testo non apre niente; **libero o preso — mai chi**. «Prenota» è di E6b, nel dialog.
  - **L'esportazione** per il Gate Manager (`GET /api/events/{slug}/bookings/export`, `Export/BookingsExport.cs`): con un token personale
    dell'`audience` **`events.bookings`** (`EventsModule.TokenAudiences`, permesso `EventBookings.View`, la parola
    `events:tokenAudiences.bookings`), chiesto anche all'unico handler sulla riga; un array con i nomi del Gate Manager (`slot_id`,
    `callsign`, `flight_number`, `booked_by`, `aircraft_icao`, `aircraft_types`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`,
    `rotation`, `leg`, `paired_slot_id` — `aircraft_types` aggiunto dopo il banco, i tipi ammessi con il principale per primo, vuoto su un
    privato, un'aggiunta alla versione 1), orari UTC con la `Z`, **nell'ordine dell'orario allo scalo dell'evento** (l'on block di un arrivo: fino alla
    revisione andava per off block, contro la nota). **Una bozza mai**: 409 `code: "draft"`; il 404 prima del 403 è voluto (revisione,
    punto 7). **La versione del contratto** (punto 9 di Carmine): l'intestazione **`Hub-Bookings-Contract: 1`**; senza, o con una versione
    che l'hub non parla, 400 `code: "bookingsContract"` con `current` e `accepted`; la controlla il `ContractVersion` del nucleo (E10g),
    dopo il token e prima dell'evento. Il contratto per chi scrive il programma è **`docs/events-bookings-export.md`** (inglese).
  - **I test**: `EventsSlotsTests` (unità, 41: il lettore, una riga, gli istanti, il verso, le catene, la finestra, i tipi del form),
    `EventsSlotsTests` (integrazione, 5 — la finestra, il volo ricaricato, l'ordine e la versione dell'esportazione dalla revisione, i tipi
    del form e `aircraft_types` dal banco —, VID 761012–761014, scali `XED1`–`XED4`, tipi `XE5A`/`XE5B`, slug `evt-test-e5-…`),
    `screens/slotList.test.ts` (vitest, 6) ed `screens/EventSlots.test.tsx` (vitest, 8: le sezioni, il tooltip al focus e al tocco, una
    pressione dimenticata, l'icona detta una volta, il dialog, il focus che torna, la selezione), due test nella smoke
    `web/e2e/events-public.spec.ts` (10, **uno su un telefono**: `hasTouch`, il tocco vero in Chromium; e il focus dopo Escape), il giro
    `web/e2e/full/events-slots.spec.ts` (il «fatta quando», con `afterwards(…)`; e il form di uno slot con i suoi due campi nel browser).
    `EventsTestRows` toglie anche gli slot; `ErasureTests` ha le due righe di `evt_slots`. VID 761015–761016 e 761062–761067 restano
    liberi.
- **Che cosa deve sapere la fase dopo**:
  - **L'esportazione è la versione 1 di un contratto** (`docs/events-bookings-export.md`): dentro una versione l'hub solo aggiunge. E6a
    riempie `booked_by` e `aircraft_icao`, che ci sono già: nessuna versione nuova. Un campo tolto o rinominato, o un significato cambiato,
    è la versione 2, accettata accanto alla 1 per almeno un rilascio; il documento cambia nella stessa PR del codice.
  - **E6a**: **«libero» si dice in un posto solo**, `SlotRows.Free` (`Staff/SlotLoading.cs`): oggi ogni slot, perché non c'è ancora una
    prenotazione; E6a lo restringe agli slot che nessuna prenotazione nomina, e «sostituisci» ed «elimina i liberi» seguono. Poi:
    `PublicEventSlotDto.Taken` (oggi `false`), `booked_by` e `aircraft_icao` dell'esportazione (oggi vuoti; `aircraft_icao` è **il tipo scelto
    dal pilota**), «uno slot prenotato non si elimina» (il `Delete` del CRUD degli slot e il primo rifiuto di `EventSaving.DeleteAsync`), e le
    righe delle colonne di persona di `evt_bookings` in `ErasureTests`.
  - **E6b**: «Prenota» va **nel dialog dello slot** (`SlotDetail` in `screens/EventSlots.tsx`), che mostra già tutti i tipi ammessi, il
    principale per primo: fra quelli il pilota sceglie il suo, che diventa `aircraft_icao` dell'esportazione. La pagina pubblica non ha una
    lettura sua: tutto viene da `PublicEventDto.Slots`, e un campo che servisse è un'aggiunta a quel DTO. ⚠️ Il dialog non ha un bottone
    suo che lo apre: **il focus torna al nominativo** grazie a `openedBy` di `EventSlots`, che lo rimette quando il dialog è sparito (dentro,
    la trappola del focus lo riprenderebbe). Un dialog che si chiude dopo «Prenota» passa da lì anche lui.
  - ⚠️ **Il tooltip che si apre al tocco** (`Hint` in `screens/EventSlots.tsx`): il tooltip di Radix si apre solo al passaggio del mouse e
    al focus, e fra la pressione e il click lo chiude e lo riapre da sé (un tocco dà il focus al bottone dopo che il dito si alza).
    Per questo il click rovescia quello che si vedeva **quando la pressione è cominciata**; un click della tastiera (`detail` 0) rovescia
    quello che si vede, e una pressione annullata si dimentica. Un semplice «al click si rovescia» lascia aperto il secondo tocco (provato
    al contrario nella smoke con `hasTouch`). **`Hint` resta un pezzo di questa schermata** (Carmine, punto 8 sulla #228): il giorno che
    una seconda schermata vuole un tooltip che si apre al tocco, è una decisione da portare a Carmine — non una copia, e non un riuso
    fatto da sé.
  - **E7**: un privato è `Kind = Private`, `Generated`, lo scalo e il verso, e l'orario allo scalo in `OffBlockUtc` (partenza) o
    `OnBlockUtc` (arrivo); l'esportazione li porta già così (gate vuoto, `paired_slot_id` vuoto finché E7 non lo legge dalla prenotazione);
    il form di uno slot li rifiuta (`events:errors.slotNotPublic`) e la scheda non dà loro «Modifica»; «elimina i liberi» li toglie.
  - **E8b** («Duplica») copia, a scelta, gli slot pubblici con le rotazioni: le colonne sono quelle di `SlotDraft`, e i posti ci sono già.
    Gli slot copiati si spostano con le date del nuovo evento, o cadono fuori dalla sua finestra (`SlotWindow`).
  - ⚠️ **Il verso si fissa quando lo slot si scrive** (`IsArrival` ed `EventAirportIcao`; revisione, punto 8): uno scalo aggiunto all'evento
    dopo lascia uno slot salvato com'era, finché qualcuno non lo salva di nuovo.
  - ⚠️ **Le date dell'evento che cambiano non ricontrollano la finestra**: uno slot rimasto fuori resta finché non si salva di nuovo
    (allora è rifiutato) o un «sostituisci» non lo toglie, se è libero.
  - ⚠️ **Le catene usano `bookingGapMinutes` di quando si caricano**: cambiare l'impostazione dopo non ricontrolla le rotazioni salvate.
  - ⚠️ **Due caricamenti dello stesso evento nello stesso momento li tiene solo l'indice univoco** (revisione, punto 6): ognuno controlla le
    catene con gli slot che legge, quindi insieme possono salvare una catena che nessuno dei due avrebbe accettato da solo. Due persone
    che caricano lo stesso evento nello stesso secondo sono un caso lontano; una catena così resta salvata, e chi corregge poi una delle sue
    tratte se la vede rifiutare finché non la ripara.
  - ⚠️ **FluentValidation: un `.When` alla fine di una catena di regole vale per tutta la catena** (`ApplyConditionTo.AllValidators`):
    scritta così, la regola del formato del callsign avrebbe spento anche «obbligatorio», e un callsign vuoto sarebbe passato. Trovato
    rileggendo, prima dei test: ora il formato sta in un `RuleFor` suo, e il test d'integrazione manda un callsign vuoto.
  - ⚠️ **Il test d'integrazione scrive un privato sul database** (nessuno lo genera ancora): E7 lo sostituisce con il generatore.
  - ⚠️ **Uno spec del giro completo dice che cosa rimette a posto con `afterwards(…)` del banco, mai in un `finally`** (#227,
    `CONTRIBUTING.md`): `events-slots.spec.ts` lo fa dal merge di `main` che è arrivato con il branch di E10g; `events-public.spec.ts` ed
    `events-staff.spec.ts` del giro completo, di E4 e di prima, hanno ancora il `finally`.

[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6021830879
[a228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040717010
[v228b]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6040474527

### Che cosa ha lasciato E10g (6–7 ottobre 2026, branch `m4/e10g-contract-version`, PR #230, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-06-la-versione-di-un-contratto-nel-nucleo.md`, **decisa**: Carmine sulla #230,
  [le sue risposte](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039777720) — sì al nucleo invece di una copia,
  il passaggio dei tour in una sua sessione, la riga in `CLAUDE.md` §2 dal master):
  - **La versione di un contratto nel nucleo**: `ContractVersion` in `src/IvaoHub.Core/Auth/ContractVersion.cs`, namespace
    **`IvaoHub.Core.Auth`**, accanto ai token personali. Si costruisce con l'intestazione del contratto, la versione corrente, le
    accettate, la chiave del titolo e il `code`. `RequireAsync` è un filtro di endpoint che risponde come quello dei tour
    (`AgentContract.RequireVersionAsync`): l'intestazione senza spazi intorno, sole cifre ASCII, fra le accettate; altrimenti **400** con
    `code`, `current` e `accepted` e il titolo nella lingua di chi chiede; se accettata, la risposta ripete nella stessa intestazione **la
    versione parlata** (non la corrente), e l'endpoint risponde.
  - **Il costruttore rifiuta** un'intestazione che non è un token di HTTP, nessuna versione, una versione non positiva o ripetuta, la
    corrente fuori dalle accettate, una chiave o un `code` vuoti: quando il modulo dichiara il contratto, all'avvio.
  - **I test**: `tests/IvaoHub.UnitTests/ContractVersionTests.cs` (unità, 21), con **il test gemello**, che confronta il nucleo con i
    valori dei tour e il filtro dei tour su 120 coppie, byte per byte.
  - Nessuna migrazione, nessun endpoint, nessuna chiave del nucleo, niente nel browser.
- **Che cosa deve sapere la fase dopo** (E5, la #228, in coda dopo questa, che porta già questo branch):
  - **Come la usa un modulo**: un campo `static readonly ContractVersion` con i valori del suo contratto — **un'intestazione sua**, non
    quella dell'agente (Carmine, punto 9 sulla #228), la versione corrente, le accettate, **la chiave del titolo con il namespace del
    modulo**, nel file delle parole del modulo in ogni lingua, e il suo `code` — e `.AddEndpointFilter(contratto.RequireAsync)` sugli
    endpoint del contratto. La policy del token risponde prima del filtro (401, 403): l'autorizzazione è un middleware, il filtro gira
    dentro l'endpoint.
  - ⚠️ **Il titolo è del modulo**: il nucleo non aggiunge parole. Senza la chiave nel file del modulo il 400 ha per titolo la chiave
    stessa, e `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer` vuole una chiave con il namespace che il file del modulo
    dichiari.
  - ⚠️ **Un endpoint aperto** che dice il contratto prima che il programma parli una versione (i tour hanno `/contract`) non mette il
    filtro e scrive da sé `Header` con `Current`: il nucleo non ha un `Announce` (nota §3). L'esportazione di E5 non ne ha uno.
  - **Il documento pubblico** del contratto è del modulo, come `docs/agent-contract.md` dei tour: l'intestazione, il 400 con `code`,
    `current` e `accepted`, la regola delle versioni (dentro una versione solo aggiunte; una rottura è la versione dopo, accettata
    accanto alla vecchia per almeno un rilascio).
  - ⚠️ **La copia dei tour c'è ancora** (`AgentContract.RequireVersionAsync`): la toglie una sessione di Carmine **dopo l'unione di
    questa PR** ([sua risposta sulla #230](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6039777720), alla
    [richiesta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6026482474); nota §5). Fino ad allora il test gemello
    tiene le due copie uguali; con il passaggio se ne va anche lui. **Sì anche alla riga in `CLAUDE.md` §2** (la versione del contratto di
    un programma esterno è `ContractVersion` del nucleo, mai un controllo del modulo), che aggiunge il master con il piano.

### Che cosa ha lasciato E4b (6 ottobre 2026, branch `m4/e4b-online-at-airports`, PR #226, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-06-chi-e-online-sugli-scali-nel-nucleo.md`, scelta tecnica sulla (b) che Carmine ha scelto
  sulla #223, punto 2; il dettaglio in `10`, E4b, «Com'è andata»; nessuna migrazione, nessun endpoint, nessun blocco nuovo):
  - **`networkStats` con gli scali che una schermata chiede**: `airports`, un elenco di ICAO nelle `props` della domanda, **fuori dallo
    schema zod** come `from` e `to` del calendario (un editor non li salva). Con l'elenco lo spazio è quello degli scali — controllori
    la cui stazione è uno di loro (la torre e l'avvicinamento di `LIRF`, non il centro `LIRR` sopra), piloti il cui piano di volo parte
    da uno di loro o ci arriva — e le due cifre `divisionAtc`, `divisionPilots` lo contano; senza, la divisione come prima. **Un elenco
    senza nessuno scalo non conta nessuno**, mai la divisione; uno scalo è da una a quattro lettere o cifre, e se ne contano al più 50,
    **dopo la pulizia** (`IvaoAirspace.OfAirports(scali, limite)`).
  - **Un whazzup al minuto per tutti** (`IvaoNetworkPicture`, in `src/IvaoHub.Core/Ivao/IvaoWhazzup.cs`): la lettura comune per un
    minuto, contata per ogni spazio con la sua `CacheKey`, che ora **nomina gli scali** quando non ci sono centri; la lettura **tiene le
    risposte di al più 16 spazi** (`MaxKeptAnswers`) e conta ogni volta gli altri. Lo scostamento dalla nota di E4 e la misura (6 ottobre
    2026: 0,8 MB, dalla cache di Cloudflare; mille insiemi inventati in 24 ms invece di mille scaricamenti) sono nella nota, §2 e §4; lo
    scostamento e le parole del titolo li porta a Carmine il revisore (rilievi sulla #226, punto 5).
  - **`<LiveStatusStrip airports={…} />`** (`web/src/shared/ui/LiveStatusStrip.tsx`): la domanda con gli scali e il titolo
    `liveStatus.airportsTitle` («Su questi scali adesso», «At these airports now»); senza `airports`, la striscia di sempre.
  - **I test**: `LiveStatusAirportsTests` (unità, 8), `NetworkStatsAirportsTests` (integrazione, 5, sulla fixture `whazzup.json`),
    `LiveStatusStrip.airports.test.tsx` (vitest, 4). Nessun VID, nessuno slug.
- **Che cosa deve sapere la fase dopo** — ⚠️ **la prima fase del modulo dopo il merge di E4b** (Carmine sulla #223, punto 2; «per esempio
  E6b» diceva la nota di E4) **monta la striscia sulla pagina dell'evento**:
  - `<LiveStatusStrip airports={gli ICAO degli scali dell'evento} />` sulla pagina di E4 (`web/src/modules/events/screens/public.tsx`),
    **il giorno dell'evento** (design §7.1): la fase scrive come legge «il giorno» (nell'ora della divisione da mezzanotte a mezzanotte, o
    dall'inizio alla fine) e lo dice in «Com'è andata». **Un evento di tutta la divisione** non ha scali e non monta niente: la striscia
    della divisione è già in cima al sito.
  - ⚠️ **Dove**: `docs/UI-GUIDELINES.md` vuole la striscia «in the banner slot of `Shell`», con una misura in `web/e2e/live-status.spec.ts`,
    e il banner del layout `_public` ha già la striscia della divisione. Dentro la pagina è una scelta della fase, da scrivere; nel banner
    al posto di quella della divisione vuole che il layout sappia gli scali della rotta: un'altra modifica del nucleo, con la sua nota.
  - **I test della fase**: una spec che ferma `networkStats` con `stubTheBlockData` e cerca `liveStatus.airportsTitle`, leggendo nella
    richiesta gli scali chiesti (`props` in base64url); sul banco la fixture `tests/fixtures/ivao/whazzup.json` ha `LIRR_CTR`, `LIMC_APP`,
    `LIRF_TWR`, `EDDF_TWR` e i voli LIRF→LIMC, EDDF→LIMC, EDDF→EGLL: un evento a `LIRF` mostra un controllore e un pilota, uno a `LIMC`
    un controllore e due piloti.
  - Il modulo **non nomina la rete**: chiede `networkStats` attraverso la striscia del nucleo, e `EventsArchitectureTests` resta com'è.

### Che cosa ha lasciato E4 (6 ottobre 2026, branch `m4/e4-public-and-routes`, PR #223, in coda dopo la #221)

- **Che cosa c'è** (il dettaglio in `10`, E4, «Com'è andata»; una migrazione additiva, `AddEventRoutes`; nessun file del nucleo oltre ai
  tre test condivisi che la fase deve toccare; quattro note nuove, tre decise da Carmine sulla #223 — [la risposta][ok223]):
  - **Le rotte** (`evt_routes`, `EventRoute`, design §1.4): da uno scalo a un altro — mai a sé stesso, `events:errors.routeToItself`
    (la revisione della #223) —, la rotta da inserire (obbligatoria, 1024 caratteri, non tradotta) e le note (`remarks_i18n`, tradotte:
    scritte in una lingua, in tutte quelle della divisione; 500 per lingua). Riga `IEventChild` nell'area **`EventRoutes`**
    (`[PermissionArea]`, `[Audited]`, lo scope dell'evento); CRUD `/api/events/routes` (`EventRoutes.View`/`.Edit`, `filter[eventId]`,
    l'evento adottato in `BeforeAuthorize`, i due scali conosciuti dal nucleo; più rotte fra gli stessi due scali ammesse). Il FOD, che
    tiene l'area sull'ED per il grant della sua posizione, scrive le rotte di ogni evento e non l'evento. **Le rotte stanno nell'ordine in
    cui il FOD le scrive** (l'`Id`), sulla pagina e nella scheda del back office, dove si riordinano per scalo a mano: l'ordine per
    partenza metteva il ritorno prima dell'andata (dal banco). **Eliminare un evento** porta via le sue rotte nello stesso salvataggio,
    con l'audit, come gli scali.
  - **La pagina** `/events/{slug}` (`GET /api/events/public/{slug}`, anonima, `Public/PublicEvents.cs`): banner, stato e tipo, quando in
    UTC e nell'ora della divisione, chi organizza (con la sua pagina), gli scali e le rotte con il nome che il nucleo conosce, la nota di
    un annullato, la descrizione. La legge chi è il pubblico dell'evento **mentre si vede** (`EventState.IsSeen` sul filtro globale: un
    evento `Members` solo a chi è entrato); **lo staff degli eventi** (`Events.View` sulla riga, chiesto all'unico handler) **in ogni
    stato**, con una riga che dice che nessun altro la vede e perché — `unseen` del DTO, da `EventState.Unseen`: bozza, non ancora
    visibile, concluso; lo stato da solo non distingue un annullato non ancora visibile da uno concluso —, più il link al back office;
    tutti gli altri **404**.
  - **Il blocco `events.eventList`** (sempre vivo, le due metà; `EventListProvider`, proprietà `kinds` e `limit`): gli eventi che si
    vedono, in arrivo e in corso, annullati compresi fino alla fine, dal più vicino. **`/events` lo legge** come `/calendar` legge il blocco
    del calendario, quindi la lista non ha un endpoint suo: schede (`screens/EventCards.tsx`, le stesse del blocco, con gli scali per
    codice e nome come la pagina: `AirportName`), filtri per tipo e per scalo nell'indirizzo (le scelte sono quelle che le schede hanno;
    lo scalo si sceglie per codice e nome, e l'indirizzo tiene il codice), il `CalendarView` sugli stessi eventi meno gli annullati. ⚠️ **Al
    più 50**: `/events` chiede al blocco `limit: 0`, e il server dà a una lista al più `PublicEvents.MaxItems` (50, il limite di una lista
    dei blocchi del nucleo, `DataBlockScope.MaxItems`), i più vicini. Una divisione che annunciasse più di cinquanta eventi insieme
    vedrebbe i cinquanta più vicini, con i filtri e il calendario su quelli; gli altri entrano man mano che i primi finiscono, e
    nessuna riga dice che ce ne sono altri. Oggi è lontano; se servisse, la pagina avrebbe una lettura sua, a pagine.
  - **`EventState.Seen(now)`**: `IsSeen` scritto in quello che SQL chiede, tenuto alla stessa risposta da `EventsStateTests`.
  - **La scheda «Rotte»** della pagina dell'evento nel back office (lista e form generati, `/staff/events/{id}/routes/{routeId}`), a chi
    legge l'area, con «Nuova rotta» a chi la scrive; e il link «La sua pagina sul sito».
  - **«Pubblica» aspetta le impostazioni salvate** (l'⚠️ che E3b lasciava a E4): spento, con una riga che dice perché, finché il form ha
    modifiche non salvate.
  - **I tipi di un evento sono quelli degli eventi** (nota `2026-10-06-i-tipi-che-un-evento-sceglie`, decisa da Carmine): quelli con una
    riga in `kindPresets`, e tutti i tipi del calendario finché non ce n'è nessuna. Il form li offre (`eventKinds` in `schemas.ts`), il
    server rifiuta gli altri su un evento nuovo o al cambio di tipo (`events:errors.kindNotOfEvents`, in `EventSaving.PrepareAsync`), e un
    evento già scritto tiene il suo tipo anche se la riga va via. ⚠️ Sul banco e sulla prova la scelta resta com'è finché le impostazioni
    degli eventi non hanno righe: per IT le scrive la divisione (RFE, RFO, MSE, Online Day e l'evento libero, con tutto spento).
  - **I test**: `EventsPublicTests` (integrazione, 4, VID 761033–761036, scali `XEC1`/`XEC2`, slug `evt-test-e4-…`), `EventsStateTests`
    (+2), un test dei tipi in `EventsStaffTests`, `screens/cards.test.ts`, `schemas.test.ts` ed `eventForm.test.ts` (vitest), la smoke
    `web/e2e/events-public.spec.ts` (8), il giro `web/e2e/full/events-public.spec.ts` (1), un terzo test in
    `web/e2e/full/events-staff.spec.ts` e, nel primo, i tipi che il form offre. `EventsTestRows` toglie anche le rotte.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Con E4 unita dopo la #221 cade l'⚠️ di E3b** («nessuna consegna e nessun «Pubblica» sulla prova fra E3b ed E4»): la voce di
    calendario e la riga di ricerca trovano la loro pagina. Quando consegnare resta di Carmine.
  - **Le tre note di E4 sono decise** da Carmine ([la risposta sulla #223][ok223], 6 ottobre 2026, autore `SkyMistery`, pubblicata dal
    master su sua istruzione): `2026-10-06-il-pubblico-degli-eventi` (sì alle cinque letture, e **la seconda lettura scritta a mano**,
    `GET /api/events/public/{slug}`, accettata come quella dei preset: uno scostamento dichiarato dal design §7.2, contato per il piano
    §16.6 — nella nota, §4), `2026-10-06-chi-e-online-sugli-scali-di-un-evento` (la `LiveStatusStrip` sugli scali dell'evento, design §7.1,
    **non è di E4**: è la fase del nucleo **E4b**, in una PR sua con la sua nota; non è urgente, può venire dopo E5, e la monta la prima
    fase del modulo dopo di lei) e `2026-10-06-i-tipi-che-un-evento-sceglie` (come raccomandato, fatta su questa PR: sopra).
  - **`search.kinds.events`** (la parola del tipo `events` nella ricerca) è arrivata con la #222 del nucleo, e il merge di `main` su questo
    branch l'ha tenuta, senza conflitti ([i rilievi sulla #223][r223], punto 3).
  - **E5** (gli slot pubblici) è **in coda sul branch di E4**: la sessione che coordina la prepara dalla testa `94ca28b` di E4, e unisce
    l'ultima spinta di E4 — con il merge di `main`, i tipi degli eventi e le risposte di Carmine. La lista pubblica degli slot va sulla
    pagina — `PublicEvents.ReadAsync` e `PublicEventDto`, oppure una lettura sua accanto —; la scheda «Slot» accanto a «Rotte»;
    `EventSlot` è `IEventChild` come le rotte. E5 non aggiunge blocchi.
  - ⚠️ **`EventSaving.DeleteAsync` elimina le righe figlie di ogni area** (scali, rotte): una fase che aggiunge una tabella figlia (slot E5,
    postazioni E11a, regole di award E14b) la aggiunge lì, e il guardiano chiederà a chi elimina l'`Edit` di quell'area — EC ed EAC tengono
    tutte le aree tranne `EventReports.Edit` (§6.2), che non ha righe figlie dell'evento.
  - **E6b** (il promemoria con la rotta del FOD) e **E8b** («Duplica» copia le rotte) trovano le rotte per evento; fra gli stessi due scali
    ce ne può essere più d'una, e le note dicono quale. L'ordine è quello di scrittura (l'`Id`): «Duplica» le copia in quell'ordine, o
    la copia le mostra in un altro.
  - ⚠️ **`EventsArchitectureTests` vieta fra apici una chiave seminata del calendario anche fuori da un tipo** — una chiave di query
    `'event'` è caduta al primo giro (ora `'page'`) — **e un ICAO di quattro maiuscole**: gli esempi della galleria usano `XX01`, `XX02` e il
    tipo `gallery`.
  - **`/events` non è nel menu pubblico**: si aggiunge dall'editor del menu, come `/tours` e `/training` (un dato, non codice).
  - ⚠️ Lo snapshot del modello degli eventi (`EventsDbContextModelSnapshot`) ha preso con `AddEventRoutes` anche la colonna `notified_at`
    di `cms_award_signals` (E10d), una tabella del nucleo mappata ed esclusa dalle migrazioni del modulo: nessun SQL, solo il modello al
    passo. La prossima migrazione degli eventi non la ritrova.

[r223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6014660539
[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

### Che cosa ha lasciato E3b (5 ottobre 2026, branch `m4/e3b-event-life`, PR #221, senza coda)

- **Che cosa c'è** (il dettaglio in `10`, E3b, «Com'è andata»; nessuna migrazione, nessun file del nucleo; dopo la revisione una nota,
  `decisions/2026-10-06-le-regole-di-pubblica.md`, decisa da Carmine: le tre regole qui sotto — pubblicabile dopo «Pubblica»,
  `bookingOpensAtUtc` per un evento con slot, il tipo `events` nella ricerca — e la scelta sui file di una bozza):
  - **«Pubblica»** (`POST /api/events/events/{id}/publish`, `Events.Edit` sulla riga): i controlli in una classe sola, `EventPublishing`
    (`src/IvaoHub.Modules.Events/Staff/EventPublishing.cs`), con i `Refusals` campo per campo — titolo e riassunto in ogni lingua, le date
    in ordine (visibile ≤ prenotazioni ≤ inizio < fine), per un evento con slot l'apertura delle prenotazioni e uno scalo (campo
    `airports`), il link per un evento di altri. Una volta; mai su un annullato; 409 su una versione vecchia.
  - **Un evento pubblicato resta pubblicabile**: `EventSaving` rifiuta una modifica con le chiavi di «Pubblica», e l'ultimo scalo di un
    evento pubblicato con slot non si elimina.
  - **La proiezione** (`Event.Project`, `IProjectable`): quando l'evento si vede (`EventState.IsSeen`: pubblicato, da `visible_from` o
    dalla pubblicazione alla fine, annullato compreso) una voce di calendario — tipo e visibilità dell'evento, `/events/{slug}` — se non
    è annullato, e una riga di ricerca per lingua (tipo `events`) se è per tutti; il banner e le immagini della descrizione come usi dei
    file: senza fine finché l'evento è una bozza, fino alla fine + 7 giorni quando è pubblicato.
  - **`events-release`** (`EventReleaseJob`, ogni 15 minuti): riproietta gli eventi pubblicati diventati visibili o conclusi
    dall'inizio dell'ultimo giro riuscito, come `TourReleaseJob`.
  - **`events.eventChanged`** dichiarato (mail e parola del profilo), senza destinatari.
  - **La schermata**: «Pubblica» nella barra della pagina di una bozza; i rifiuti come elenco «campo: che cosa manca» sotto la barra.
  - **I test**: `EventsLifeTests` (integrazione, 6, VID 761011, scalo `XEB1`, orologio dell'host spostato dal test), `EventsStateTests`
    (+1), un secondo test in `web/e2e/full/events-staff.spec.ts`; `EventsTestRows.ForgetAsync` toglie gli eventi di una classe con scali
    e proiezioni (lo usa anche `EventsStaffTests`).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Nessuna consegna e nessun «Pubblica» sull'installazione di prova fra E3b ed E4** (Carmine, 6 ottobre 2026, [sulla #221][seq221]):
    la voce di calendario e la riga di ricerca di un evento pubblicato puntano a `/events/{slug}`, che porta solo E4.
  - ⚠️ **«Pubblica» pubblica la riga salvata**: il form delle impostazioni si ridisegna sulla riga pubblicata (la sua chiave è la
    versione della riga), e una modifica non salvata si perde senza un avviso (dalla lettura del codice, non provato nel browser; il
    revisore l'ha lasciato fra i non verificati). La fase che tocca di nuovo la pagina (E4, con la scheda delle rotte) può spegnere
    «Pubblica» finché il form ha modifiche.
  - **E4** trova in `EventState.IsSeen` la regola di chi vede un evento (il pubblico: pubblicato, visibile e non concluso; annullato
    compreso, con la nota) e l'indirizzo `/events/{slug}` che calendario e ricerca usano già: la pagina pubblica deve rispondere lì.
    ⚠️ Un evento `Members` è nel calendario dei membri e mai nella ricerca (§8.1).
  - ⚠️ **Il tipo della riga di ricerca è `events`** (la chiave del modulo): `event` è una parola seminata del calendario, e
    `EventsArchitectureTests` vieta al modulo di scriverne una. Il distintivo della ricerca mostra la chiave finché `common.json` (nucleo)
    non ha `search.kinds.events`, come oggi `tour`.
  - ⚠️ **Ogni scrittura di un evento pubblicato** passa da `EventPublishing`: una fase che aggiunge un controllo a «Pubblica» lo aggiunge
    lì, e vale anche per le modifiche; una riga figlia la cui assenza rende l'evento non pubblicabile (come l'ultimo scalo) rifiuta il
    suo `Delete` allo stesso modo.
  - ⚠️ **E6a** manda `events.eventChanged` dopo il salvataggio di un evento pubblicato i cui orari cambiano (il punto è
    `EventSaving`), con i segnaposto proposti `{{event}}`, `{{starts}}`, `{{ends}}`, `{{url}}` o altri suoi.
  - ⚠️ **Gli eventi scritti prima di E3b** (banco, installazione di prova) non hanno proiezioni finché non si salvano: oggi sono bozze, e
    il banner diventa un uso alla prossima scrittura.
  - ⚠️ **Un test che sposta l'orologio** usa un host derivato (`WithWebHostBuilder` + `ConfigureTestServices` con un `IClock` suo, come
    `EventsLifeTests`), mette in pausa il job nello scheduler, toglie le righe di `hub_jobs_log` del job all'inizio e non fa richieste
    firmate mentre l'orologio è spostato.

[seq221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670

### Che cosa ha lasciato E3a (1 ottobre 2026, branch `m4/e3a-event-staff`, PR #214, in coda dopo la #212)

- **Che cosa c'è** (il dettaglio in `10`, E3a, «Com'è andata»; nessuna migrazione; una nota, dopo la revisione:
  `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`, decisa da Carmine):
  - **Lo stato dalle date** in una funzione sola, `EventState.Of` (`src/IvaoHub.Modules.Events/EventState.cs`), e le cinque viste della
    lista (`EventViews`, `filter[view]`: bozze, prossimi, in corso, conclusi, annullati), scritte anche in SQL e tenute alla stessa
    risposta da `EventsStateTests`. Ogni istante è il primo momento di ciò che apre; annullato vince su tutto, bozza su ogni data.
  - **L'evento nel CRUD** (`/api/events/events`, `src/IvaoHub.Modules.Events/Staff/`): `Events.View`/`Edit`, `DeletePolicy`
    `Events.Delete`; `EventSaving` (indirizzo libero, tipo del calendario e attivo quando lo si sceglie, niente scali su un evento di
    tutta la divisione; eliminare porta via gli scali); **gli scali** (`/api/events/airports`, `EventAirportEndpoints`): righe
    `IEventChild` che adottano dipartimento e maschera dell'evento in `BeforeAuthorize` (`EventChildren.AdoptAsync`), un aeroporto del
    nucleo una volta, la capacità in movimenti oppure in arrivi e partenze.
  - **Due endpoint a mano**: **annulla** (`POST /{id}/cancel`, `Events.Edit` sulla riga: quando, chi, la nota in ogni lingua; una volta;
    mai su un evento concluso; 409 su una versione vecchia) e **i preset dei tipi** (`GET /api/events/kind-presets`, `Events.Edit`):
    il form li legge per preimpostare gli interruttori al cambio del tipo, perché le impostazioni le legge solo chi le gestisce.
  - **Le schermate**: `/staff/events` (lista con il filtro delle viste), `/staff/events/{id}` (impostazioni, descrizione con l'editor
    dei blocchi, scali; «Annulla l'evento» ed «Elimina» a chi il server lo permette), `/staff/events/{id}/cancel`,
    `/staff/events/{id}/airports/{airportId}`. Il tipo di notifica **`events.eventCancelled`** è dichiarato, senza destinatari.
  - **I nove grant di chi collabora** (AOD, FOD, MD) nei due file della divisione, e `EventsArchitectureTests` che li vuole.
  - **I test**: `EventsStateTests` (unità, 6), `EventsStaffTests` (integrazione, 7, VID 761006–761010, aeroporti `XEA1`/`XEA2`),
    `eventForm.test.ts` (vitest, 4), `web/e2e/full/events-staff.spec.ts` (il personaggio dell'ED: un RFO con due scali creato,
    riaperto, annullato; una bozza vuota eliminata).
- **Che cosa deve sapere la fase dopo**:
  - **E3b** trova pronti gli stati che «Pubblica» accende (`Scheduled`, `Announced`, `BookingOpen` vengono da `Status == Published` e
    dalle date) e le viste; «Pubblica» è un verbo accanto ad «Annulla» in `EventEndpoints`, con i `Refusals` del design §2.2.
    ⚠️ Sul banco oggi ci sono solo bozze: le viste «prossimi», «in corso», «conclusi» le prova l'integrazione con righe scritte sul
    database.
  - ⚠️ **`SchemaForm` disegna i default una volta**: il form dell'evento si ridisegna (chiave nuova, stessi valori) quando cambia il
    tipo; l'indirizzo proposto dal titolo smette di seguirlo dopo il ridisegno se il titolo c'era già.
  - ⚠️ **E11a ed E16** portano `has_roster` e `in_person` nel form e anche in `presetSwitches`, `EventWriteDto` ed `EventMapper.Apply`:
    oggi il form ne applica tre.
  - ⚠️ **Ogni riga figlia nuova** (rotte E4, slot E5, postazioni E11a, regole di award E14b) implementa `IEventChild` e adotta l'evento
    con `EventChildren.AdoptAsync`; nel browser `writableDepartments` non offre l'ED a chi collabora (E2b): la riga si crea sotto
    l'evento. Chi collabora vede oggi la pagina dell'evento con il form senza «Salva» e gli scali.
  - ⚠️ **E6a** mette il primo rifiuto di «Elimina» in `EventSaving.DeleteAsync` e i destinatari di `events.eventCancelled` dopo il
    salvataggio in `CancelAsync`; la fase che manda la prima di quelle mail **rimette sulla pagina «Annulla»** la frase su chi la riceve
    (`events:cancel.description` e l'aiuto della nota), tolta dopo la revisione della #214 perché oggi non parte nessuna mail.
  - ⚠️ **Il banner non è ancora un uso di un file** (`Event` non è `IProjectable`): fino a E3b un banner si può eliminare dalla libreria
    mentre un evento lo mostra (rilievo 4 del revisore sulla #214). E3b subito dopo.
  - **La lettura dei preset** (`GET /api/events/kind-presets`) **resta com'è**: Carmine l'ha accettata il 1 ottobre 2026
    ([la sua risposta sulla #214](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5934118725)), uno scostamento dal
    design §7.2 («gli endpoint a mano sono verbi») scritto nella nota `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`; nessun
    permesso di lettura in `ModuleSettingsDescriptor`. ⚠️ Una fase che vuole un'altra impostazione del modulo nel form di chi non le
    gestisce non la aggiunge qui da sola: è un endpoint a mano nuovo, con la sua decisione (piano §16.6).
  - ⚠️ **Nel back office `PageShell` non disegna la `description`** (solo il tooltip del titolo): lo stato dell'evento sta nella `note`.
  - ⚠️ **`AirportEndpoints` è un nome del nucleo** (`Core/Ivao`): le classi del modulo si chiamano `Event…`.
  - La spec e2e scrive un preset dell'RFO nelle impostazioni del banco e lo rimette com'era, come `events-skeleton.spec.ts` con l'RFE.
  - ⚠️ **Il tipo di un evento si sceglie fra tutte le parole del calendario**, anche training, esame, tour, riunione e scadenza (come i
    preset di E2): nessun tipo del calendario dice di essere di un evento, e il codice non ne conosce nessuno. Un elenco più stretto
    sarebbe una domanda a Carmine e un segno del nucleo, non una lista nel modulo.

### Che cosa ha lasciato E2b (1 ottobre 2026, branch `m4/e2b-grant-without-department`, PR #212, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-il-permesso-non-il-dipartimento.md`, **decisa da Carmine** il 30 settembre, in chat al
  master e pubblicata su sua istruzione [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)):
  - **Un grant a una posizione su un dipartimento che non è il suo** — su un altro, o su tutti — e **un grant al team di un FIR** con
    `firStaffScope: all` danno **il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, scritto dal calcolo
    (`UserGrant.GivesThePermissionNotTheDepartment`) e portato nel claim da un `!` in testa al pezzo dello scope (`Events.View:ED@!`), che un
    lettore che non lo conosce legge chiuso.
  - **Chi lo tiene** non ha il claim `dept` di quel dipartimento (`HubClaims.BuildIdentity`): nessuna riga `Visibility.Department`, nessun
    gruppo nella barra dello staff, nessun'altra lista di quel dipartimento. **La lista generata** che legge con quel permesso ne tiene le
    righe (`TryNarrowToDepartments`, accanto a `onTheirFir`); **l'unico handler e il guardiano** lo tengono sulle righe del dipartimento come
    ogni permesso, e non sono cambiati.
  - **Un grant a una persona** fa ancora entrare nel dipartimento (6 settembre); **lo stesso permesso da fuori e per nome** resta quello
    per nome.
  - **I test**: `PermissionFromOutsideRulesTests` (unità, 8) e `PermissionFromOutsideTests` (integrazione, 4, VID 761091–761094; 761090 è
    un'identità delle unità; 761095–099 restano di E2b per le sue correzioni). **Le parole**: l'aiuto del form dei permessi
    (`grants.formHint`), `docs/FORKING.md` e i commenti di `config/division.example.json`. Nessuna migrazione, nessun endpoint.
  - **Tenuto da fuori su tutti i dipartimenti** (un grant a una posizione senza `scope`), un permesso di lettura dà nella sua lista le
    righe di ogni dipartimento, come i claim `dept` di tutti prima di E2b, e niente altro di loro; un divieto su un dipartimento accanto
    toglie quel dipartimento (dopo la revisione della #212).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I nove grant di chi collabora non ci sono**, nemmeno dopo l'unione di E2 (#209) in questo branch: li porta **E3a** (`10`, E3a
    punto 5), la cui sessione nasce sopra E2b, in coda dopo la #212, in `config/division.json` e in `config/division.example.json`, e cambia
    `EventsArchitectureTests` di E2, che li rifiuta finché la nota non ha risposta — ora ce l'ha. **Misurato** sopra E2b con E2 e i nove
    grant: integrazione intera 443/443, i tre test del maintainer verdi senza toccarli; fra le unità l'unico rosso è quel test di E2.
  - **Che cosa vedranno AOD, FOD e MD** (nota §3.7): gli eventi e le liste della loro area, con le righe dell'ED, ogni riga con l'unico
    handler, la sezione «Eventi»; non le righe che l'ED tiene per sé, non il gruppo dell'ED nella barra, non i suoi contatti né i suoi
    contenuti.
  - ⚠️ **`writableDepartments`** nel browser non offre il dipartimento di un permesso da fuori: una schermata che fa creare una riga a chi
    collabora (una rotta del FOD) la crea sotto l'evento, con maschera e scope dell'evento, e chiede al server le `actions`.
  - ⚠️ **Un test che prova che cosa può chi collabora** entra come una posizione di quel dipartimento, senza indirizzo, e mai come il web
    master del banco, che raggiunge ogni dipartimento; dopo aver scritto un grant rifà l'ingresso.
  - **E10f (#213) e E2b sono riconciliate in questo branch** (E10f è entrata prima): **un solo `EffectivePermission.FromOutside`** con le
    due vie nel suo `<param>`, una condizione sola nel calcolo — `fir is null && (grant.GivesThePermissionNotTheDepartment ||
    catalogue.IsGlobal(grant.Value))` —, un solo `.ThenBy(FromOutside)`, una sola riga in `BuildIdentity` con i due commenti, e le due
    aggiunte in `docs/FORKING.md` e in `config/division.example.json`. ⚠️ **Il segno ora viaggia anche per i globali**: chi assegna gli
    award per grant ha nel cookie `Awards.Assign@!` e `Awards.View@!`. Ogni riga di `Award` resta aperta: il catalogo è condiviso in
    lettura (`Award` è `ISharedForReading`, la sua lista dice `SharedForReading = award => true`) e l'unico handler risponde a una lettura
    con `HasAny(Awards.View)`, che non guarda né il dipartimento né il segno; e `Awards.View@!` si rilegge senza dipartimento, cioè su
    tutti (`10`, E2b, «Com'è andata»).
  - **Il team di un FIR con `own`** (IT) non cambia: il suo permesso porta il FIR, e il suo claim è quello di prima. ⚠️ Un cookie scritto
    prima di E2b tiene i suoi claim `dept` fino al prossimo ingresso; su IT nessuno ne ha uno da un grant a una posizione su un altro
    dipartimento.

### Che cosa ha lasciato E10c (1° ottobre 2026, branch `m4/e10c-ratings-and-positions`, PR #204, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione.md`, **decisa da Carmine**, in chat
  al master e pubblicata su sua istruzione [sulla #204](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5916282164)),
  tutto in `src/IvaoHub.Core/Ivao/`:
  - **il rating preferito** per un tipo di postazione: **la regola è della divisione**, `config/division.json → preferredAtcRatings`
    (tipo di postazione → sigla del rating ATC; [seconda risposta di Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926811688)),
    controllata all'avvio (`PreferredAtcRatingsValidator`: un tipo o un rating sconosciuti fermano l'hub con un messaggio); il
    nucleo risponde `RatingVocabulary.PreferredFor(tipo)` dal vocabolario che registra (`IvaoRatings.WithPreferred`). I valori di IT
    sono di Carmine: **AS3** su `DEL`; **ADC** su `FSS`, `GND`, `TWR`; **APC** su `APP`, `DEP`; **ACC** su `CTR`; **nessuno** su `ATIS`.
    «Ha il preferito» è `IsAtLeast(Atc, rating, PreferredFor(tipo).Number)`; `null` = nessuno è preferito su quel tipo;
  - **le postazioni della divisione**: `IAtcPositionDirectory.OfDivisionAsync()` (tutte, militari e ATIS compresi) e
    `FindAsync(nominativi)` (a lotti, per nominativo in qualunque maiuscola, come `IAirportDirectory.FindAsync`); **`AtcPositionDto`
    porta il tipo** (`Type`), accanto a nominativo, nome, aeroporto e **FIR** (per una postazione d'aeroporto, quello del suo aeroporto);
  - **il minimo di una postazione è il suo FRA su IVAO**: `MinimaAsync(nominativi)` → un `AtcPositionMinimum` per nominativo, e
    `Over(da, a)` dà il **numero di IVAO** del rating più alto fra gli FRA attivi che valgono in qualche momento del turno (giorno e
    notte, feriali e fine settimana, una data, oltre la mezzanotte); `null` = nessun minimo. Gli FRA stanno in **`ref_ivao_fras`**
    (migrazione del nucleo `AddIvaoFras`, dopo quella di E10d), rinfrescati **ogni notte** con i dati di riferimento, solo le righe per
    postazione (**nessuna persona**); **senza risposta di IVAO restano, con una risposta vuota se ne vanno** (la divisione li ha tolti
    tutti: il rilievo 2 del revisore); il conteggio, o «nessuna risposta», è nel messaggio del giro, non nel suo esito;
  - **le parole dell'avviso** sotto il minimo — togli l'FRA su IVAO per quel controllore —: la chiave del nucleo
    `atcPositions.belowMinimum` (`AtcPositionMinimum.BelowMinimumKey`), perché nominano IVAO;
  - **le misure**: `tools/record-ivao-fixtures.mjs --fras <paese> [ICAO…]`, e la fixture `tests/fixtures/ivao/fras-IT.json` (94 FRA
    delle postazioni del banco, 30 settembre 2026);
  - **i test**: `RatingVocabularyTests`, `IvaoFraReaderTests`, `AtcPositionMinimumTests` e `PreferredAtcRatingsValidatorTests`
    (unità), `AtcPositionTests` (integrazione).
- **Che cosa deve sapere la fase dopo**:
  - **E11a**: la scelta delle postazioni con `OfDivisionAsync()`, il controllo del nominativo e la copia del FIR con `FindAsync`; il
    tipo non va salvato sulla riga (lo sa la directory).
  - **E11b**: «chi può» = almeno `minimumAtcRating` **e** almeno `minima[callsign].Over(turno.da, turno.a)` quando non è `null`, sempre
    con `IsAtLeast` (un numero che il vocabolario non conosce non lo raggiunge nessuno); il proponente **non va mai sotto**. «Chi prima»
    = `PreferredFor(position.Type)`. **La correzione a mano può andare sotto l'FRA**: è un **avviso**, mai un rifiuto, con le parole di
    `atcPositions.belowMinimum` (decisione di Carmine sulla #204); lo stesso per la cessione di un turno (E12).
  - ⚠️ **Il minimo è un numero di IVAO** (`int?`), non un `Rating`: confrontalo con `IsAtLeast`, mai con `>=`.
  - ⚠️ **`IvaoRatings.Vocabulary` (statico) è la scala di IVAO da sola**, senza la regola della divisione: `PreferredFor` vi risponde
    sempre `null`. Il modulo prende il `RatingVocabulary` dal contenitore, che la porta; i suoi test costruiscono un vocabolario di
    prova con la loro mappa (`new RatingVocabulary(ratings, preferred)`).
  - ⚠️ **Gli orari degli FRA si leggono UTC**, come ogni orario di IVAO: non verificato (nessuna fonte lo dice).
  - ⚠️ **Le eccezioni per membro restano su IVAO**: un controllore che lo staff ha già sbloccato su IVAO resta «sotto il minimo» per
    l'hub, e l'avviso compare lo stesso.
  - ⚠️ **L'ordine dei nominativi è quello del database** (`utf8mb4_unicode_ci`: `_` prima delle cifre, `LIRR_NE_CTR` prima di
    `LIRR_NE1_CTR`): un test non lo confronti con un ordinamento `Ordinal`.
  - ⚠️ **E15a ed E10a** toccavano gli stessi file del client di IVAO (`IIvaoApiClient.cs`, `IvaoApiClient.cs`,
    `FixtureIvaoApiClient.cs`), lo strumento delle fixture e il suo README: entrate in E10c con due merge il 1° ottobre, lo strumento ha
    ora le quattro modalità di riferimento (`--positions`, `--bookings`, `--fras`, `--sessions-at`) e il README tutte le sezioni. **E10c
    migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione sopra `main`
    (come ha fatto E10c dopo E10d).

### Che cosa ha lasciato E10f (1 ottobre 2026, branch `m4/e10f-grantable-award-assign`, PR #213, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-chi-assegna-gli-award-con-un-grant.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643), risposta 2):
  - **`Awards.Assign` si dà con un grant, ed è il solo globale così**: il campo `PermissionDescriptor.GrantableAlthoughGlobal`, detto sul
    permesso come `DeniedToStakeholder`, vero solo per `Awards.Assign` in `CorePermissions`. Il catalogo risponde con
    `IsClosedToGrants(name)`, la domanda che fanno il calcolatore, la schermata dei permessi e il seme; e **non nasce** se lo dicesse un
    altro permesso (`Permissions.Manage` per primo, ma anche un altro globale, un permesso di un dipartimento, un globale di un modulo:
    il paletto che il revisore proponeva sulla #213, preso).
  - **Solo intero**: il calcolatore lo tiene (su nessun dipartimento, come un ruolo) solo da un grant senza dipartimento, senza scope e
    non al team di un FIR; un rifiuto intero lo toglie anche a chi lo ha per ruolo (DIR, ADIR, WM, AWM). La schermata rifiuta il dipartimento
    (**`errors.grant.globalDepartment`**, chiave nuova), il team di un FIR (`firTeamArea`, com'era) e gli altri globali
    (`globalPermission`, con un testo nuovo); il seme salta uno `scope`. **`ModuleGrants` rifiuta ancora ogni globale.**
  - **Il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, vero per un grant di un permesso globale e per la
    `Awards.View` che porta; `HubClaims.BuildIdentity` lo lascia fuori dai claim `dept`. Senza, un grant senza dipartimento mette chi lo
    tiene **dentro tutti i dipartimenti** (l'ha trovato la sessione di E2b; misurato: `user.departments` del coordinatore dell'MD
    passava da `["MD"]` a tutti e nove).
  - **La divisione**: `config/division.json` dà `Awards.Assign` al **coordinatore e all'assistente dell'MD** (primo dei `positionGrants`,
    senza `scope`); lo stesso in `config/division.example.json`, e `docs/FORKING.md` spiega l'eccezione.
  - **Il browser**: `/api/me → registries.permissions[].grantableAlthoughGlobal`; la schermata dei permessi offre `Awards.Assign`.
  - **I test**: `GrantableGlobalPermissionTests` (unità, 17), `AwardsAssignByGrantTests` (integrazione, 4, VID 761080–761083),
    `web/src/features/admin/grants/grantable.test.ts` (2); **tolto il caso `Awards.Assign`** da
    `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` (test di Carmine: il cambio l'ha deciso lui).
- **Che cosa deve sapere la fase dopo**:
  - **Il riepilogo di E10d arriva all'MD da solo**, dal primo avvio dopo il rilascio: il seme nuovo si applica una volta, poi si cambia
    dalla schermata dei permessi. **E14b** non cambia niente.
  - ⚠️ **Nei test d'integrazione l'MD ha `Awards.Assign`**, perché l'host legge `config/division.json`: un coordinatore o un assistente
    dell'MD seminato da un test (i contatti seminano `IT-MC` con un indirizzo) è fra chi assegna. `AwardQueueMailTests` conta per le sue
    persone e non se ne accorge; un test nuovo che conta i destinatari del riepilogo faccia lo stesso.
  - **Le tre scelte della sessione sono confermate da Carmine**
    ([sulla #213](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926811970)): coordinatore e assistente dell'MD; il
    rifiuto intero che vale anche per chi lo ha per ruolo; la `Awards.View` su ogni dipartimento, perché chi assegna veda ogni award.
  - ⚠️ **`FromOutside` è lo stesso campo di E2b** (PR #212, decisa sulla #209: un grant a una posizione su un dipartimento non suo dà il permesso
    e non il dipartimento), con la stessa forma: ultimo parametro di `EffectivePermission`, la stessa preferenza nella deduplicazione, la
    stessa riga in `BuildIdentity`. **Il significato non è identico** (il revisore sulla #213): in E2b il segno scrive anche un `!` nel
    cookie, e con tutte e due unite la `Awards.View` portata da `Awards.Assign` si scrive `Awards.View@!`. **Chi arriva seconda su `main`**
    tiene una dichiarazione sola, somma le due condizioni del calcolatore e rifà sul codice unito `AwardsAssignByGrantTests`,
    `GrantableGlobalPermissionTests` e le due classi di E2b; il revisore controllerà che quella `Awards.View` apra ancora ogni riga di
    `Award`. E2b porta anche la metà «liste»: fino ad allora **chi non è dentro nessun dipartimento** (il capo di un FIR a cui si desse
    `Awards.Assign` con un grant a un VID) apre la coda ma ha la lista degli award a 403. L'MD non ne è toccato.
  - Un **permesso globale di un modulo** resta chiuso ai grant: dire `GrantableAlthoughGlobal` ferma l'avvio. Un secondo permesso
    concedibile è una decisione con la sua nota, che cambia anche il paletto del catalogo.

### Che cosa ha lasciato E10a (30 settembre – 1° ottobre 2026, branch `m4/e10a-tracker-without-vid`, PR #210, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-tracker-senza-vid.md`, scelta tecnica; le misure del 30 settembre sono lì, §2, e
  la revisione in §6):
  - **La domanda senza VID**: `IvaoSessionQuery(int? Vid, from, to, departure, arrival, IvaoConnectionType? ConnectionType)` con
    `Limit` (`init`, predefinito 200, **al più `MaxLimit` = 1000**, un tetto che tiene il nucleo); `SearchSessionsAsync` è la stessa
    di prima, dalla più recente, senza doppioni, al più `Limit`, `null` quando IVAO non si è potuto chiedere;
    `IvaoTrackerSessionDto.ConnectionType`. Il giro delle pagine e la regola del tracker stanno nel lettore
    (`IvaoTrackerReader.ReadPagesAsync`, `Answers`), per il client vero e per quello delle fixture.
  - **Il client di IVAO aspetta 20 s per tentativo** (l'interruttore campiona su 40, il totale resta 30): con i 10 standard nessun
    aeroporto si leggeva. **Confermato da Carmine** con il `null` per la ricerca dei tour
    ([risposta sulla #210](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5917033792)).
  - **Le fixture**: `tools/record-ivao-fixtures.mjs --sessions-at`; `tests/fixtures/ivao/tracker-airport-LIRF.json` (una sera di
    LIRF: 10 sessioni di 9 membri, la torre) e `tracker-pages-LIRF.json` (le stesse partenze come le pagina IVAO), **spostate sul 1°
    gennaio 2001** (lo stesso giorno delle prenotazioni di E15a) e **senza `rating`, `serverId`, `software*`** (revisione, #210).
    Senza VID, `FixtureIvaoApiClient` risponde dal file dell'aeroporto chiesto. Unita dopo E15a, lo script ha le due modalità
    (`--bookings` e `--sessions-at`) e **un giorno inventato solo**, `standIn` con `movedFrom(day)`, in cima: chi registra altre righe
    con persone lo usa.
  - **I test**: `IvaoTrackerWithoutVidTests` e `IvaoApiTimeoutTests` (unità).
- **Che cosa deve sapere la fase dopo** (E13a, e chiunque legga il tracker senza VID):
  - ⚠️ **Una domanda per aeroporto che trova qualcosa costa ~10,5 s** (la pagina con l'ultima riga, sempre), e **due insieme
    ricevono 504** dal gateway di IVAO: una alla volta, poche per giro del job.
  - ⚠️ **La finestra è sull'inizio della sessione**, estremi compresi: per chi era già connesso, `FromUtc` va allargato.
  - ⚠️ **«Partenza o arrivo» sono due domande**: chiesti insieme, i due aeroporti vogliono la stessa revisione del piano. Una
    sessione può tornare da tutte e due: si conta per `Id`.
  - ⚠️ **Il DTO dice gli aeroporti della prima revisione**: trovata per la partenza da LIRF, una sessione può dire LIPZ.
  - **Il limite si dichiara**: con esattamente `Limit` sessioni la risposta può essere tagliata, e il resto sta prima della più
    vecchia.
  - **Le VID 761020–761028 sono le persone della sera di LIRF** nella fixture (761025 due volte), e **la sera sta sul 1° gennaio
    2001**, 16:00–17:59:59 UTC: un test che la legge chiede quel giorno. 761029 non è usata.
  - Il punto «La richiesta al tracker oggi» di «Per chi prende M4», qui sopra (`PageSize = 50`, `MaxSessions = 200`), è superato:
    le pagine sono da 100 e il tetto è il `Limit` di chi chiama, al più 1000.
- ⚠️ **Per i tour cambiano due cose, nessuna nei loro test**: le pagine sono da 100, e se IVAO non risponde affatto la ricerca dà
  `null` («tracker non disponibile») invece di lanciare.

### Che cosa ha lasciato E15a (30 settembre 2026, branch `m4/e15a-network-atc-bookings`, PR #207, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-prenotazioni-atc-della-rete.md`, scelta tecnica; le misure con il token vero sono
  lì, §2; **nessuna cache e al più sette giorni per richiesta confermati da Carmine** dopo la revisione, [risposta sulla
  #207](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/207#issuecomment-5916738183)):
  - **La domanda del modulo**: `IAtcBookingSource.BookedAsync(fromUtc, toUtc, callsign?)` in `src/IvaoHub.Core/Ivao/AtcBookingSource.cs`
    (namespace `IvaoHub.Core.Ivao`, che il modulo importa come fa il training: nessun nome del file o del tipo nomina IVAO) → le
    prenotazioni della rete che **si sovrappongono** alla finestra, di tutte le postazioni o di un nominativo intero, nell'ordine in cui
    cominciano; **`null` = non disponibile**, mai «nessuno ha prenotato». `AtcBookingDto(Callsign, StartsAt, EndsAt, Vid, Kind)`, con
    `AtcBookingKind` `Controlling`, `Training`, `Exam`. Al più **`IAtcBookingSource.MaxDays` = 7** giorni di UTC (oltre: `null` e un
    avviso nel log). Alla richiesta, **nessuna cache**, nessun job.
  - **Il client**: `IIvaoApiClient.GetDailyAtcBookingsAsync(DateOnly, position?)` (`/v2/atc/bookings/daily`, il giorno di IVAO com'è;
    predefinito «non disponibile» per i doppi dei test), `IvaoApiClient` con il token dell'applicazione, `FixtureIvaoApiClient`, e **un
    lettore solo** per tutti e due, `IvaoAtcBookingReader` (`IvaoAtcBookings.cs`).
  - **Lo strumento**: `tools/record-ivao-fixtures.mjs --bookings <nome> <asVid> <yyyy-mm-dd> <prefisso…>`, con la persona tolta e
    niente che ritrovi la prenotazione attraverso l'API di IVAO: né `id` né `createdAt`, e il giorno spostato sul 1° gennaio 2001.
  - **La fixture** `tests/fixtures/ivao/atc-bookings-day.json` (VID 761070–761079; il giorno vero non è scritto da nessuna parte) e il
    suo paragrafo nel README delle fixture.
  - **I test**: `AtcBookingTests` (unità, 25 casi), provati al contrario (`10`, E15a, «Com'è andata»).
- **Che cosa deve sapere la fase dopo** (E15b):
  - **Una domanda per l'evento, non una per postazione**: `BookedAsync(inizio, fine)` senza nominativo, poi le postazioni dell'evento
    con un filtro: una chiamata a IVAO per giorno di UTC invece di una per postazione. Il nominativo della domanda, quando c'è, è
    **intero** (con `LIRR` non torna niente: per IVAO sarebbero tre settori).
  - ⚠️ **IVAO lento o giù**: prima di «non disponibile» il client aspetta quello che aspetta ogni chiamata a IVAO (i tentativi e il
    tempo massimo della resilienza, 30 secondi per chiamata), una chiamata per giorno. Carmine lo ha accettato così: la pagina aspetta e
    poi dice che le prenotazioni non sono disponibili, **mentre il resto del roster funziona** — quindi **le prenotazioni si caricano a
    parte dal roster** (una richiesta loro), e il roster non aspetta IVAO.
  - **Il VID, non il nome**: un membro dell'hub con `personName`, chi non è mai entrato con il suo numero. `Exam` e `Training` dicono
    che la postazione è occupata da un esame o da un training su IVAO: vale la pena mostrarlo.
  - **Sul banco (fixture) ogni giorno è lo stesso giorno registrato**: `LIRF_TWR` 18–20 (761079), `LIMC_TWR`, `LFPG_APP`,
    `LIMM_WS2_CTR`, `LFFF_CTR`, `LIRR_SU_CTR`, `LIRR_NC_CTR` 19–21, `LIRR_NE_CTR` 19–22, l'esame di `EDDF_APP` 18:30–20:30, `SBGR_TWR`
    23–01 (UTC). I VID
    761070–761079 non sono nessuno sul banco: la pagina mostra i numeri. Una spec che vuole vedere un controllore del roster anche fra
    le prenotazioni ha bisogno di una fixture con il VID di un personaggio del banco (il roster prende solo chi è entrato nell'hub).
  - ⚠️ **E10a tocca gli stessi file nello stesso giorno** (`IIvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, lo script delle fixture):
    chi viene unita per seconda fonde `main` quando il master lo chiede (mai un rebase), e tiene tutte e due le modalità dello script.
- ⚠️ **Trappole trovate** (nota, §2): la `position` di IVAO è **il principio del nominativo** (`LI` = tutte le italiane); una
  prenotazione a cavallo della mezzanotte è nell'elenco di **tutti e due** i giorni, una che finisce alle 00:00 anche del giorno dopo;
  `date` con un'ora che non è 00:00 dà un elenco vuoto; `user` porta anche `rating`, che la documentazione non dice. ⚠️ **Una
  prenotazione registrata ritrova la persona** con l'API di IVAO dal suo `id`, e anche dal suo giorno con il nominativo (rilievo del
  revisore): chi registra altre prenotazioni lo fa con lo strumento, che toglie tutti e due. La prima registrazione, con quei dati, resta
  nella storia del branch (`fbb11ac`): fuori da `main` la tiene solo un merge a squash, che decide il maintainer.

### Che cosa ha lasciato E2 (30 settembre – 1 ottobre 2026, branch `m4/e2-events-skeleton`, PR #209, la prima fase del modulo)

- **Che cosa c'è** (il dettaglio in `10`, E2, «Com'è andata»):
  - **Il modulo** `src/IvaoHub.Modules.Events/` (referenzia solo il nucleo), registrato **per primo** in `IvaoHub.Web/Modules.cs` e in
    `web/src/modules/index.ts` (eventi, tour, training: la sezione Eventi per prima nel back office, **confermata da Carmine**
    [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)); `EventsDbContext` con
    `__EFMigrationsHistory_events` e la migrazione
    **`Initial`** (`20260930170049_Initial`), che **non si tocca più**: `evt_events` **intera** e `evt_event_airports`.
  - **Le entità** alla radice del progetto: `Event` (maschera, `IVisible`, `IPublishable`, audit, `[Audited]`, scope
    `events:event:{id}` con `Event.ScopeOf`) ed `EventAirport` (maschera, audit, lo scope del suo evento). Nessun endpoint, nessuna
    proiezione, nessun validatore del CRUD ancora.
  - **Il catalogo** `EventsPermissions` (12 permessi, 5 aree, `DeniedToStakeholder` su `EventAtc.Edit` ed `EventReports.Edit`) e **i
    grant** dell'ED (11) e del team di un FIR (2, su `EventAtc.*`) nei due file della divisione.
  - **Le impostazioni** `EventsSettings` (`Settings/EventsSettings.cs`: `kindPresets`, `bookingGapMinutes`, `pilotRetentionMonths`,
    `reminderLeadHours`) con i validatori, e la schermata generata `/staff/events/settings` (`web/src/modules/events/`), l'unica voce
    della sezione «Eventi» del back office.
  - **Il segmento riservato** `events`; le parole `events` in `web/src/modules/events/locales/` (e le copie in `locales/`).
  - **I test**: `EventsArchitectureTests` ed `EventsSettingsTests` (unità), `EventsSkeletonTests` ed `EventsXxDivisionTests`
    (integrazione, VID 761001–761005), `web/src/modules/events/schemas.test.ts`, `web/e2e/full/events-skeleton.spec.ts` (il personaggio
    dell'ED, `?as=events`); le cinque righe `evt_` in `ErasureTests`, con la nota breve
    `2026-09-30-le-colonne-degli-eventi-in-erasuretests`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I grant di AOD, FOD e MD non ci sono, e aspettano E2b.** Un grant sull'ED fa entrare chi lo tiene nell'ED per tutto quello che
    vede (`HubClaims.BuildIdentity`), e i nove del design sarebbero i primi grant a una posizione fra due dipartimenti: con loro la suite
    d'integrazione intera ha tre rossi del maintainer (`SeveralDepartmentsTests` ×2, `SearchEndpointTests`). **Carmine ha deciso la (b)**
    della nota `2026-09-30-i-grant-di-chi-collabora-sugli-eventi`
    ([sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)): un grant a una posizione su un
    dipartimento che non è il suo dà il permesso, non il dipartimento, in una fase del nucleo a sé, **E2b**, prima di E3a; i nove si
    seminano dopo di lei. `EventsArchitectureTests` li tiene fuori dai due file finché la tabella del design non li riprende: **un grant
    del seme applicato non si toglie più togliendolo dal file**. E3a, che prova FOD e AOD sull'evento, aspetta E2b.
  - ⚠️ **I test d'integrazione degli eventi avviano l'host con `useIvaoFixtures: true`** (l'avviso di E10b qui sotto): senza, un host
    chiede un token a IVAO quando `ref_ivao_centers` è vuota. `EventsSkeletonTests` si riprende alla fine di ogni test i grant e le
    posizioni che dà ai suoi VID; i membri restano, come in ogni classe.
  - **Le colonne di `evt_events`**: `starts_at_utc` ed `ends_at_utc` obbligatorie, `visible_from_utc` e `booking_opens_at_utc`
    facoltative, `shift_minutes` e i tre limiti vuoti = l'impostazione, `visibility` è una colonna (E3a la chiude a `Public` e `Members`
    nel validatore), `kind` lunga come una chiave del calendario (32). Le regole di «Pubblica» sono di E3b.
  - **E3a** scrive l'interfaccia delle righe figlie dell'evento (come `ITourChild`) con `CrudOptions.BeforeAuthorize`, e aggiunge
    `AddValidatorsFromAssemblyContaining` con la prima risorsa del CRUD (come A5 per il training): E2 non li ha.
  - **Le due verifiche del design §6.3** non chiedono il nucleo: l'intestazione per lo staff dei FIR da un endpoint dell'area ATC con
    `EventAtc.View` senza risorsa, le disponibilità per postazione con l'unico handler chiesto sulla postazione (`10`, E2, «Le due
    verifiche»). Le scrive in codice E11a.
  - ⚠️ **Fino a E11a ogni avvio scrive due avvisi** sui grant del team di un FIR («not applied: no row of its area says its FIR»): è
    voluto; il primo avvio con una riga `IHasFir` dell'area `EventAtc` li applica.
  - **La schermata delle impostazioni** offre, accanto ai tipi del calendario, un tipo che un preset nomina e il calendario non ha più
    (con la sua chiave come parola), perché la riga si veda e si tolga: il salvataggio la rifiuta sulla riga.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`** in nessun worktree (30 settembre): le spec la tollerano, come in CI.

### Che cosa ha lasciato E10d (30 settembre 2026, branch `m4/e10d-award-assigner-mail`, PR #205, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643)):
  - **Il riepilogo a chi assegna gli award**: `AwardQueueMailJob` (`award-queue-mail`, `src/IvaoHub.Core/Awards/`) gira una volta al
    giorno all'ora di **`division.json → awardDigestTime`** (`HH:mm` nell'ora della divisione, **07:00** se manca; IT non la scrive).
    Legge la coda del nucleo e, **solo se** sono entrati segnali nuovi, manda a chi ha `Awards.Assign` (`IPermissionHolders`,
    superadmin compresi) **una mail**: quanti segnali nuovi, quanti in attesa, una riga per motivo e award proposto con il numero,
    **senza VID**, il link a `/staff/awards/queue`. Tipo del nucleo **`award.toAssign`**: nel profilo da solo, spegnibile.
  - **Il segno** `cms_award_signals.notified_at` (migrazione del nucleo `AddAwardSignalNotifiedAt`; le righe già in coda sono segnate
    come dette): un segnale si racconta una volta sola, nello stesso salvataggio delle righe della mail, e uno gestito o scartato prima
    del giro non si racconta.
  - **Per ogni modulo**: il job non guarda `source_module`, e il modulo dei tour non è cambiato.
  - **I test**: `AwardQueueMailTests` (integrazione: un segnale dei tour e uno di prova; chi l'ha spento e chi non ha il permesso; una
    volta sola; l'ora dell'host), `AwardQueueMailLinesTests` e `AwardDigestTimeTests` (unità).
- **Che cosa deve sapere la fase dopo**:
  - **E14b** proietta i segnali e basta, senza chiamare niente; la mail parte all'ora di `awardDigestTime` dopo il segnale. Il suo
    «fatta quando» sul banco è il segnale in coda: il banco non ha nessuno con `Awards.Assign` e una casella (il web master non ne
    ha), e il job gira solo alla sua ora. La mail la prova `AwardQueueMailTests`.
  - ⚠️ **Oggi l'MD non ha `Awards.Assign`**: è un permesso globale, e un grant non dà mai un permesso globale (nota §5). Coda e mail
    sono di DIR, ADIR, WM, AWM e dei superadmin. **Carmine ha deciso** che diventi concedibile, **in una fase del nucleo sua** (la
    prepara la sessione che coordina, non E10d): quando ci sarà, la mail arriverà all'MD da sola.
  - ⚠️ **Gli snapshot dei contesti dei moduli** vedono `notified_at` solo al loro prossimo `migrations add` (lo scarto innocuo di
    T4b). Una fase che fa nascere o migra un contesto dopo E10d se la trova nello snapshot come tabella esclusa: è giusto così.
  - ⚠️ **In un test d'integrazione un job del nucleo gira da solo nell'host**: un test che lo fa girare lo mette prima in pausa
    (`ISchedulerFactory`, come `TourTests`) e passa `useIvaoFixtures: true` (l'avviso di E10b qui sotto).
  - ⚠️ **E10d migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione
    sopra `main`.

### Che cosa ha lasciato E10e (30 settembre 2026, branch `m4/e10e-great-circle-core`, PR #206, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo.md`, scelta tecnica, con una richiesta a
  Carmine):
  - **La distanza fra due aeroporti nel nucleo**: `GreatCircle.DistanceNm(GeoPoint, GeoPoint)`, in miglia nautiche, e
    `GreatCircle.DistanceNmRounded`, al decimo (una metà va al decimo pari), con `GeoPoint(Latitude, Longitude)` in gradi — in
    `src/IvaoHub.Core/Airspace/GreatCircle.cs`, namespace **`IvaoHub.Core.Airspace`**. È il codice dei tour con i loro numeri: la
    distanza fra i due aeroporti, non le miglia volate.
  - **Come la usa un modulo**: chiede a `IAirportDirectory.FindAsync` dove sono gli aeroporti (un `FindAsync` solo per tutte le voci),
    prende le coordinate con un pattern (`is { Latitude: { } …, Longitude: { } … }`: un aeroporto senza coordinate non ha distanza) e
    misura con `GreatCircle`. Nessuna registrazione, nessuna migrazione, nessun endpoint.
  - **I test**: `tests/IvaoHub.UnitTests/GreatCircleTests.cs` (unità, 8): le domande di `LegTests` al nucleo con le stesse risposte,
    gli antipodi e l'antimeridiano, e **il test gemello**, che confronta il nucleo e la copia dei tour bit per bit su 35.721 coppie.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Il namespace è `IvaoHub.Core.Airspace`, non `IvaoHub.Core.Ivao`** (accanto ai contorni dei FIR, geometria che non è di IVAO):
    in `Ivao` gli stessi nomi fanno cadere la build dei tour (`CS0104` su `TrackChecks.cs`, provato; nota §2 punto 3).
  - ⚠️ **La copia dei tour c'è ancora** (`src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs`): la toglie una sessione di Carmine
    **dopo l'unione di E10e** ([sua risposta sulla #206](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916695685),
    alla [richiesta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916051005); nota §5). Fino ad allora il test
    gemello tiene le due copie uguali; con il passaggio se ne va anche lui. **Sì anche alla riga in `CLAUDE.md` §2** (la distanza fra
    due aeroporti è `GreatCircle` del nucleo, mai una copia), che aggiunge il master.
  - **E14a ed E14b** la trovano qui: la distanza di una voce di un PIREP con `DistanceNmRounded` se la colonna è al decimo, come
    `fo_legs.distance_nm`, e `MinLegDistance` confrontata con quel numero.
  - Agli antipodi `h` può passare 1 di un'unità nell'ultima cifra, ma la radice lo riporta a 1: nessun NaN, misurato (nota §6).

### Che cosa ha lasciato E10b (30 settembre 2026, branch `m4/e10b-shared-sessions-by-vid`, PR #208, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-sessioni-condivise-per-vid.md`, scelta tecnica, nessuna domanda nuova), tutto in
  `src/IvaoHub.Core/Atc/`:
  - **il VID in ogni presenza**: `AtcPresence.Vid` (`int?`, una proprietà `init` sotto il costruttore), dalla colonna `vid` della
    vista; `null` dove l'archivio non lo dice (un doppio di un test), che non è il VID di nessuno. Non esce dai tour (le loro risposte
    si costruiscono campo per campo): il contratto OpenAPI non cambia;
  - **la domanda nuova** `IAtcActivitySource.SessionsOfAsync(vid, fromUtc, toUtc)`: le connessioni di un controllore aperte
    nell'intervallo, nella forma della domanda dei tour (`AtcActivity`: le presenze e da quando l'archivio è completo, per la divisione
    e per il mondo). Un controllore per domanda: vIPI indicizza `(UserId, StartUtc)`;
  - **«non disponibile»** (`null`) con `atcData.source: none`, con un archivio scritto prima di E10b (la domanda ha un corpo
    predefinito nell'interfaccia, come `IIvaoApiClient.GetAtcPositionsAsync`), con un archivio che non si legge;
  - **corretto**: con `atcData: vipi` e senza `ConnectionStrings:AtcData` **l'hub non partiva** — il contesto della vista si
    costruiva con la sorgente, fuori dal suo `try`, e all'avvio il seeder dei contenuti la costruisce attraverso i blocchi dei tour —;
    ora parte e risponde «non disponibile» (nota §4, un commit a sé);
  - **i test**: `AtcActivitySourceTests` (integrazione), con la vista di vIPI in **un database suo** e all'utente dell'hub solo il
    `SELECT` sulla vista.
- **Che cosa deve sapere la fase dopo**:
  - **E11b** (l'esperienza, design §4.3): una domanda per candidato, `SessionsOfAsync(vid, adesso − experienceMonths, adesso)`, e
    `Of(callsign)` per una postazione. «Più spesso» (sessioni o minuti) e «quel tipo» sono del modulo, e **il tipo di un nominativo lo
    dà la directory di E10c**, non questa interfaccia (la colonna `position` della vista non passa). `null` = il criterio non c'è; una
    lista vuota = nessuna esperienza.
  - **E13a** (la presenza, §4.5): `SessionsOfAsync(vid, turno.da, turno.a).Of(callsign)` per il titolare; chi ha coperto:
    `OnlineAsync(turno.da, turno.a).Of(callsign)` con un `Vid` diverso dal titolare. ⚠️ I minuti dentro il turno li conta il modulo:
    una connessione ancora aperta ha `EndedAt` nullo, una riconnessione sono due connessioni, e la prima può cominciare prima del
    turno. ⚠️ `Covers(callsign, turno.da)` falso vuol dire «non si sa» (`Unknown`), non un no-show. Con `null` il ripiego è il tracker
    per VID con `connectionType=ATC` (E10a): la scelta fra le due fonti è del modulo.
  - ⚠️ **`Vid` è `int?`**: `presence.Vid == turno.ControllerVid` con un `null` non conferma nessuno, ed è voluto.
  - ⚠️ **E10b non tocca `Core/Ivao/`**, a differenza di quello che dice «Per chi prende M4» (con E10a ed E15a): l'archivio è
    `Core/Atc/`, e non è il client di IVAO.
  - ⚠️ **Un host dei test d'integrazione senza `useIvaoFixtures` chiede un token a IVAO** quando `ref_ivao_centers` è vuota (la
    sincronizzazione all'avvio, `HubPipeline`): rifiutato con un 400, ma sono chiamate vere, cinque a ogni avvio finché la tabella
    resta vuota. I test degli eventi che avviano un host passino `useIvaoFixtures: true`, come `AtcPositionTests` e
    `AtcActivitySourceTests`.
  - `docs/FORKING.md` e `config/division.example.json` descrivono l'archivio per i tour, e restano veri: la metà degli eventi la scrive
    E15b.

### Che cosa ha lasciato E1 (30 settembre 2026, branch `m4/e1-calendar-kinds`, PR #200, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md`, scelta tecnica, nessuna domanda nuova):
  - **I quattro tipi degli eventi nel calendario**: `rfe`, `rfo`, `mse` e **`online-day`** in `seed/calendar-kinds/kinds.json`, blu come
    `event` e subito dopo (`sort` 11–14), con le etichette «RFE», «RFO», «MSE», «Online Day» (`seed.calendarKinds.*` in
    `locales/*/seed.json`). Arrivano anche a un database già avviato, e una chiave scritta a mano prima resta com'è: il seeder lo
    faceva già (A2 di M3), nessun codice cambiato. Il bootstrap (`/api/me` → `calendarKinds`) li porta a chiunque, visitatori compresi.
  - **Il coordinatore degli eventi sul banco**: `POST /e2e/signin?as=events`, VID **999005**, «Bench Events», posizione **`IT-EC`**,
    nessuna casella di Mailpit, nessun rating né ora. Oggi ha solo quello che la matrice dà a un coordinatore sul suo dipartimento
    (contenuti, link, media, calendario dell'ED); **i permessi degli eventi glieli dà il seme dei `positionGrants` di E2**.
  - **I test**: `CalendarKindSeedTests` (integrazione), `CalendarKindsXxDivisionTests` (integrazione, il fork «XX» su
    `ivaohub_xx_kinds`), `CalendarKindSeedFileTests` (unità: ogni tipo del seme passa dal validatore del back office),
    `web/e2e/full/events-bench.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **La chiave dell'Online Day è `online-day`**, non `onlineDay` come scrivono il design, la nota `i-tipi-di-evento` e il piano: la
    chiave di un tipo ha la forma di uno slug, e il back office la rilegge a ogni salvataggio. **Confermata da Carmine** il 30
    settembre ([risposta sulla #200](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912372176)). **E2** scrive
    `online-day` nei test di `kindPresets` (le chiavi che esistono nei tipi), **E3a** la trova nel bootstrap; nessun codice del modulo
    nomina un tipo.
  - ⚠️ **Le spec degli eventi entrano con `?as=events`**, non come il web master del banco: `IT-WM` raggiunge ogni dipartimento e ha
    ogni permesso di ogni modulo, quindi con lui una spec passa con qualunque `positionGrants`. Dopo E2 il personaggio ha i permessi
    degli eventi dal seme dei grant; ⚠️ un grant scritto fa rientrare il titolare (`CONTRIBUTING.md`, «Traps»): una spec che ne scrive
    uno rifà `/e2e/signin?as=events`. Quando una mail dello staff degli eventi dovrà arrivare a lui, la fase che la manda gli dà una
    casella (`E2EStaffOptions` non ne ha: la classe col campo `Email` è quella del trainer).
  - **E2 tenga d'occhio `web/e2e/full/events-bench.spec.ts`**: afferma `departments: ['ED']` e `hasAllDepartments: false` per il
    personaggio; un grant di E2 con uno `scope` diverso da ED per l'`IT-EC` (la nota `chi-lavora-sugli-eventi` non ne prevede) la
    farebbe cambiare.
  - Il commento di `TrainingSettings.ConflictKinds` («the online day joins when M4 makes its kind») è ora vero a metà: il tipo c'è,
    `online-day`; il predefinito resta `["event"]` e la divisione lo aggiunge dalle impostazioni del training.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`**: né la cartella principale né gli altri worktree hanno `tiles/basemap.pmtiles` (30
  settembre); le spec che disegnano una mappa tollerano il 404 di `/tiles/`, come in CI, quindi il giro passa lo stesso.

### Che cosa ha lasciato E0 (29 settembre 2026, branch `m4/e0-decisions`, PR #184)

- **Che cosa c'è**: dieci note in `decisions/`, una per decisione o gruppo coerente di §17 del design, ognuna con il link al
  commento di Carmine sulla #180 che la decide e con «Da portare nel piano» — `i-tre-blocchi-e-che-cosa-resta-fuori-da-m4`,
  `i-tipi-di-evento`, `chi-lavora-sugli-eventi`, `gli-slot-e-le-prenotazioni`, `la-vita-di-un-evento`, `il-roster-atc`,
  `dopo-l-evento-e-gli-award`, `gli-eventi-in-presenza`, `le-impostazioni-degli-eventi`, `i-dati-dei-membri-negli-eventi`. Il piano
  1.24 porta già quasi tutta la §18 del design: ogni nota dice che cosa c'è e che cosa resta. E `10-piano-implementazione-m4.md`: le
  regole di tutte le fasi e le fasi E0–E17, con E3, E6, E11, E13, E14 in due PR, E10 in cinque PR del nucleo (E10a–E10e), E15 in due
  (E15a del nucleo), E8 in due (E8a del nucleo, poi **tolta** nel piano 1.25 dopo A12a di M3); «Com'è andata» di E0 è già scritto.
- **Che cosa deve sapere la fase dopo**: le note di E0 registrano le decisioni, **non** la forma nel codice delle estensioni del
  nucleo: ogni fase del nucleo (E1, E10a–E10e, E15a) porta **la sua nota nuova**. ⚠️ **`core-guard` non giudica le PR di
  `SkyMistery`**, quindi il check è verde anche senza la nota: la regola la tiene chi scrive.
- **La cancellazione dei dati di una persona nasce con M4a** (E8b; E8a poi tolta) invece che in E15: proposta di E0, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184) (`10`, E0,
  scostamento 4).
- ⚠️ **Quello che il design dava per esistente e non c'è** (`10`, E0, «Trovato», punti 6–10): il tracker vuole il VID e non dice il
  tipo di connessione; le sessioni condivise non hanno il VID; il vocabolario dei rating non conosce `GND`, `DEL`, `DEP` né il minimo
  di una postazione; la directory delle postazioni cerca solo per rating; la distanza sta nel modulo dei tour. Ognuno ha la sua fase
  del nucleo, prima delle fasi di M4b che lo usano.
- ⚠️ **IT gira con `atcData: none`** (`config/division.json` non ha la chiave): senza sessioni condivise il roster perde il criterio
  dell'esperienza. È una scelta di configurazione di Carmine, da chiedere in apertura di E11b.
- ⚠️ **Prima del primo evento vero (E9)** servono la produzione (piano §15) e il recupero dei giri persi dei job nel nucleo (nota
  `2026-09-28-i-job-quando-passenger-spegne-l-hub`, non ancora nel codice).
- ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della cartella principale.
