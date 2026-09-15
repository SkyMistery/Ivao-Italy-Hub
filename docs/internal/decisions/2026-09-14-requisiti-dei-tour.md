# I requisiti dei tour, raccolti con Carmine

**Data:** 13–14 settembre 2026 — conclusioni di Carmine e dello staff FOD, confrontate con `Ivao Italy
Toursystem` (letto il 13 settembre: `CLAUDE.md`, `doc/PROPOSTA.md`, `doc/ADR.md`, `db/schema.sql`,
`doc/RULESET_PROPOSTA_IT.md`, `data/hq/`) e con il piano dell'hub.
**Stato:** **requisiti decisi** da Carmine; sono l'ingresso di `05-design-m2.md`, che li trasforma in
modello dati, schermate, permessi e fasi. Le decisioni di meccanismo (§8) hanno ciascuna la sua nota.
**Aggiornamento del 15 settembre**: le revisioni del design hanno aggiunto e precisato molte cose (aereo di riferimento, METAR
e TAF, tour `Open`, agente del validatore, ban, chiarimenti, immagini con scadenza, GDPR…). **Dove questo documento e il design
non coincidono, vince `05-design-m2.md`** (§15 raccoglie le decisioni con la data).
**Il sistema entra in uso nel 2027**, quando i tour oggi su `tours.th.ivao.aero` saranno chiusi: nessun
import dello stato attuale.

## 1. Lato pilota

- **I tour attivi dell'anno sono visibili a tutti**, anche senza login. Ogni tour è un riquadro con foto di
  sfondo, titolo, **barra di avanzamento** e **prossima leg da volare** (se il pilota ha iniziato).
- **La pagina del tour** mostra tutte le leg e la **mappa** (blu da fare, verdi fatte) e permette il report.
  Mappa con OpenStreetMap o un servizio simile (§8).
- **Il primo PIREP vale come iscrizione** al tour.
- **Il report**: il sistema cerca nel tracker IVAO degli ultimi **X giorni** i voli fra i due aeroporti della
  leg e il pilota sceglie il suo. **X lo decide il FOD**, ed è lo stesso numero di giorni concesso per
  inviare il PIREP. Poi il pilota inserisce, se richiesti, **SID, STAR, IAP** e le **autorizzazioni
  particolari ricevute dall'ATC** (per esempio free speed). Un volo del tracker vale per **una sola leg**.
- **Esenzioni da ATC**: dichiarano quali controlli ammorbidiscono e valgono solo se quell'ATC risultava
  online (come Toursystem, «vediamo come viene»).
- **Deviazioni**: ammesse e **motivate**; il PIREP porta sia la tratta con la deviazione sia il volo di
  riposizionamento, e il motivo.
- **Esiti**:
  - **approvata**: compare come approvata;
  - **da modificare**: il pilota può correggere **tutto**, anche la sessione del tracker; il PIREP torna in
    coda **a chiunque**, come uno nuovo; intanto il pilota continua a volare il tour. Se poi è bocciata, la
    tratta si rivola;
  - **rifiutata**: compare come rifiutata, con un tasto **«contesta»** che manda un messaggio al validatore.
- **Contestazione**: **solo sui rifiuti**. Il messaggio arriva alla casella del FOD; risponde chiunque del FOD,
  advisor compresi, e il validatore che ha deciso anche se non è del FOD (§8).
- **Mail a ogni esito**, nella lingua del pilota, attraverso il servizio notifiche del nucleo.
- **Il pilota vede quale regola ha violato, mai i contatori.**
- **Segnalare un problema su una leg** (per esempio un aeroporto chiuso), che lo staff può trasformare in una
  modifica.
- **Pulsante «Pianifica su SimBrief»** e **briefing** del tour (testo ricco).

## 2. Regole ed errori

- **Regole generali** (valgono per tutti i tour) e **regole del tour**. Un tour può **emendare** una regola
  generale oltre ad aggiungerne di sue: **individuali > generali**. Le regole di un tour si possono copiare da
  un altro tour.
- **Errori**, associati alle regole **molti a molti**. Un errore può non avere regole e una regola può non
  avere errori, ma **il sistema lo segnala**.
- **Ogni errore ha** un nome breve e riconoscibile; una descrizione ed esempi per il validatore; una
  **categoria**:
  - **info**: errore leggero, nessun massimo;
  - **warning**: al massimo **N volte nell'anno solare** (N si decide creando l'errore); oltre, il sistema
    **suggerisce** il rifiuto;
  - **dangerous**: suggerisce il rifiuto **dalla prima occorrenza**.
- **I contatori** sono del pilota, **su tutti i tour**, per l'anno solare corrente (e da sempre, per il
  pannello del validatore).
- **La soglia è un suggerimento**: decide il validatore, e la scelta resta registrata.
- **Gli errori possono essere pubblici**: un **blocco** che il FOD mette in una pagina o in un documento
  quando vuole.
- **Controlli automatici collegati agli errori**: un controllo può avere più errori; il sistema suggerisce al
  validatore gli errori da segnare, per aiutare i meno esperti. Solo i controlli con i dati IVAO (e l'archivio
  ATC di vIPI); rotta, procedure e Eurocontrol restano fuori (livelli B/C di Toursystem).
- **Regole congelate sul PIREP**: un PIREP si giudica con la versione delle regole di quando è stato inviato.

## 3. Validazione

- **Nessuno valida i propri PIREP.**
- **Validatori abilitati per tour**: possono validare **solo i tour per cui sono abilitati**, e vedere le leg di
  tutti i tour **in sola lettura**. Un validatore può non essere del FOD. Li abilitano **coordinator e
  assistant FOD** dalla pagina delle statistiche dei validatori («aggiungi validatore», scegliendo un VID che
  sia già staff della divisione).
- **Presa in carico**: un validatore che apre una leg la tiene per un tempo limitato, gli altri la vedono in
  lavorazione.
- **La pagina di validazione** ha tutte le informazioni della leg e un riquadro con: il profilo del pilota
  (leg volate, rifiutate e accettate **in quel tour**); la **lista degli errori** con accanto quante volte sono
  stati segnalati al pilota **nell'anno solare** e **da sempre**. Il validatore segna gli errori commessi,
  aggiunge note e accetta, rimanda da modificare o rifiuta.
- **Tolleranza sul rifiuto**: le leg successive già volate prima della decisione restano valide (Toursystem:
  12 ore).

## 4. Costruire i tour

- **Una pagina con tutti i tour** passati, presenti e futuri.
- **Creazione**: tipo di tour, regole individuali (o copiate da un altro tour), nome, foto di sfondo, **aerei
  consentiti**, eventuali **hub**, **leg massime al giorno**.
- **Template di tour** che il FOD crea, modifica ed elimina (solo coordinator e assistant), da usare come base
  **al posto della clonazione della stagione precedente**.
- **Le leg**, con un **editor dedicato** (§8):
  - partenza e destinazione; il sistema prende da IVAO **IATA e posizione** dei due aeroporti e calcola la
    **distanza GCD**;
  - **callsign reale** con cui il volo è operato (se c'è), **numero di volo** e aerei;
  - la **rotazione** a cui appartiene;
  - «la successiva **duplica** questa» (tutto uguale senza aeroporti) oppure «la **segue**» (la destinazione di
    questa è la partenza della successiva, la destinazione della successiva da inserire);
  - **«chiudi tour»**: questa è l'ultima leg e la sua destinazione diventa la partenza della prima (un tour può
    anche non chiudersi dove è iniziato);
  - **import di leg da XLSX o CSV**.
- **Il callsign è un vincolo**, impostabile **per tour, per sottotour o per leg**, anche come elenco di
  callsign vietati.
- **Date**: **data di rilascio** (il tour è disponibile da solo) e **data di chiusura** (non più disponibile),
  con una **tolleranza** pari ai giorni per inviare il PIREP: il tour risulta chiuso ma accetta i PIREP di voli
  con data **non successiva** alla chiusura. **Anche una singola leg può avere una data di rilascio**; in un tour
  in sequenza il pilota si ferma alla leg precedente finché non esce.
- **Un tour dura un anno o una parte** (per esempio da marzo a novembre); può stare **su due anni**.
- **Pubblicazione**: un tour segnato **pronto** viene pubblicato alla data di rilascio.
- **Impostazione per tour**: le leg successive si possono volare **prima** che la precedente sia validata,
  oppure bisogna **aspettare** la validazione.
- **Leg modificata dopo la pubblicazione**: con motivazione registrata. Una leg tolta **si ritira**, non si
  cancella, e i suoi PIREP restano. I tour degli anni passati restano con l'esito di ogni PIREP.
- **Limite giornaliero**:
  - per tour (leg massime al giorno);
  - **di divisione**, contato sul giorno UTC del decollo, **disattivabile**. Se è disattivato, **ogni tour deve
    avere il suo limite**: se c'è anche un solo tour attivo o futuro senza limite, il sistema lo segnala e
    **impedisce** di disattivare quello di divisione finché quei tour non ce l'hanno.
- **Manovre obbligatorie** (per esempio il touch-and-go nei tour VFR): **da fare dopo il sistema principale**.

## 5. I tipi di tour

1. **In sequenza**: dalla prima all'ultima leg, in ordine.
2. **Libero**: il pilota sceglie la leg che vuole.
3. **A hub**: ogni leg appartiene a una **rotazione** (di 2, 4 o 6 voli, dall'hub all'hub) e ogni rotazione a un
   **hub**. Il pilota sceglie un hub, ne vola **tutte** le rotazioni (ogni rotazione **in ordine**) e poi passa a
   un altro. Gli hub possono essere **collegati** da un volo (il pilota può scegliere solo un hub collegato, e
   quel volo va fatto e conta come una leg normale) oppure no (sceglie liberamente). Il tour si completa con
   **tutti** gli hub.
4. **In sequenza con partenza a scelta**: il pilota sceglie al primo PIREP la leg di partenza, poi vola in ordine
   fino all'ultima, riparte dalla prima e chiude alla leg prima di quella di partenza.
5. **A distanza**: si completa facendo N miglia (previsto, «potrebbe servire»).
6. **Con sottotour**: l'award richiede di completare **N sottotour**, non necessariamente tutti. Il «N leg su M»
   vale **solo** per i sottotour.

## 6. Pagine dello staff

- **Statistiche dei validatori**: per anno leg validate, accettate, rifiutate; e per ogni tour dell'anno corrente
  e del precedente, per ogni validatore, leg validate, accettate e rifiutate. Da qui si abilitano i validatori.
- **Pagina del pilota** (solo staff): riepilogo di tutti i warning e l'elenco di tutte le leg volate con l'esito.
- **Punti e classifiche**: **solo per lo staff**, mai pubblici.
- **Audit** di tutto.

## 7. Fuori dal modulo, o dopo

- **Award**: il modulo **segnala**, chi ha `Awards.Assign` assegna. Si costruiscono nel nucleo catalogo e
  assegnazioni; gli award si prendono dall'API IVAO se possibile, altrimenti si caricano (e più avanti i badge
  dei rating per il training).
- **Online Day**: meccanismo simile, ma passa all'**ED** (con gli eventi).
- **Conservazione**: il registro disciplinare **mai** cancellato; il resto **2 anni** dalla chiusura per i tour
  normali, **4** per quelli su più di un anno (da confermare quando si decide lo storico).
- **Discord**: rinviato. **Pilot Life**: a sistema finito si valuta.
- **Controlli di livello B/C** (rotta, procedure, Eurocontrol): fuori.

## 8. Che cosa chiede al nucleo (decisioni di meccanismo)

Ognuna ha, o avrà, una nota sua prima del codice:

1. **Permessi con scope per tour** (validatori abilitati): un'estensione del meccanismo unico dei permessi, non
   un controllo scritto nel modulo.
2. **Contestazione con risposta**: i contatti di oggi non hanno risposte; un filo di messaggi legato al PIREP.
3. **Mappa**: OpenStreetMap o simili, con l'eccezione in `config/security.json` per le tessere; un componente
   nuovo nell'elenco chiuso.
4. **Aeroporti**: IATA e coordinate promosse a colonne della tabella `ref_` degli aeroporti.
5. **Tracker IVAO**: sessioni, piani di volo e tracce nel client unico.
6. **Editor delle leg** dedicato, con import XLSX/CSV: un'eccezione dichiarata al «form generato».
7. **Award nel nucleo**: catalogo e assegnazioni accanto alle segnalazioni che esistono già.
8. **Motore dei controlli automatici** lato server, con il collegamento controllo → errori suggeriti.
9. **Archivio ATC**: letto da vIPI (`2026-09-14-dati-condivisi-con-vipi.md`).
