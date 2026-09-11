# La barra laterale, i sottomenu e il carattere di IVAO

**Data:** 11 settembre 2026 — sette richieste di Carmine nello stesso messaggio, con davanti il codice
HTML della home di va.ivao.aero.

**Stato:** **tutte e sette fatte.** Nessuna era (c): la barra laterale è già nostra dal 10 settembre
(`StaffSidebar`, ventunesimo dell'elenco chiuso), i sottomenu esistevano già, e i due caratteri
Atmosphere li chiedeva da sempre. Due delle sette hanno trovato **difetti veri**, uno dei quali mio del
giorno prima: sono la parte da leggere.

---

## Le sette, in breve

1. **A barra compatta, un dipartimento la apre su quel dipartimento.** Prima era un interruttore
   che cambiava qualcosa dentro un pannello troppo stretto per mostrarlo.
2. **Si accende solo la voce giusta.** Vedi sotto: era un difetto, e aveva due strati.
3. **Un dipartimento aperto alla volta.** Lo stato è della cornice, non del gruppo. ⚠️ La scelta
   ricorda **su quale pagina è stata fatta**: se si naviga altrove — dalla palette ⌘K, per esempio,
   in un altro dipartimento — si apre da solo il gruppo della pagina nuova, senza un `useEffect` che
   rincorra una prop (che `react-hooks/set-state-in-effect` rifiuta, e con ragione).
4. **La ricerca in cima alla barra, con accanto il pulsante per compattare.** `StaffSidebar` ha preso
   uno slot, `top`, invece di importare la ricerca: è un componente condiviso e la ricerca è una
   feature, e il condiviso non entra mai in `features/` (design M0 §6.5). A barra compatta la ricerca
   diventa un'icona, con nome e tooltip; la palette dietro è la stessa.
5. **I nomi dei dipartimenti per esteso.** «ED» diventa «Events», la sigla resta nel riquadro. ⚠️ Chi
   usa il back-office da un anno scrive la sigla nella palette: il gruppo si porta dietro il `code`, e
   un test lo verifica. **Il test ha trovato un difetto mio**: la ricerca cercava la frase intera, e
   «ED links» non è una sottostringa di «ED Events Links». Ora conta ogni parola per conto suo, che è
   anche la regola migliore («links events» trova la stessa voce).
6. **I sottomenu nell'header c'erano già** — una voce di menu con figli diventa una tendina, da G8.
   ⚠️ **Ma li avevo resi illeggibili io il 10 settembre**: portando il menu sulla barra blu ho forzato
   bianchi *tutti* i link al suo interno, compresi quelli della tendina, che ha un pannello chiaro.
   Bianco su bianco. Vedi sotto.
7. **Il carattere di va.ivao.aero**, chiesto dal PRD di IVAO. Vedi sotto: erano gli stessi due che
   Atmosphere dichiara, e che nessuno caricava.

## I due difetti, perché valgono la lettura

### La voce accesa: due strati dello stesso errore

Sui documenti di un dipartimento restava accesa anche la **dashboard**. L'indirizzo della dashboard è
la radice del dipartimento, e ogni indirizzo del dipartimento comincia con quella: un controllo «il
percorso inizia con questo indirizzo?» dice sì a tutte e due. La correzione è chiedere **quale
corrispondenza è la più specifica** (la più lunga): una sola, e una pagina di dettaglio accende
ancora la lista a cui appartiene.

⚠️ **Il secondo strato non si vedeva.** Misurato nel DOM: il router marcava `aria-current="page"`
**sia** la dashboard **sia** i documenti — chi usa uno screen reader sentiva due «pagina corrente». E
per la stessa ragione la home del sito, `/`, era «corrente» su ogni pagina. Corretto nell'unico
adattatore dei link (`RouterAnchor`), con `exact` e `includeSearch: false` — senza il secondo non
corrispondeva più niente, perché le liste mettono paginazione e ordinamento nell'indirizzo.

### La tendina bianca su bianco, e il test che passava lo stesso

C'era **già** un e2e che apriva «About» e verificava che «Team» fosse `toBeVisible()`. È rimasto verde
col testo bianco su bianco, perché per Playwright «visibile» vuol dire che ha una scatola, non che si
legge. Ora lo stesso test **misura il contrasto**, ed è stato verificato che fallisca col difetto
rimesso (1,06 : 1).

⚠️ E la misura ha avuto un difetto suo, che è la ragione per cui ora è **una funzione sola**
(`e2e/contrast.ts`): il primo tentativo leggeva lo sfondo `oklab(… / 0.2)` della tendina con
un'espressione regolare e dava 1,3 : 1 a del testo scuro su bianco — la trappola che
`contrast.spec.ts` descriveva già nel suo commento. Adesso i due spec dipingono i colori su un canvas
nello stesso modo.

## Il carattere

va.ivao.aero usa **Poppins** per il testo e **Nunito Sans** (800) per titoli e nome della divisione.
Atmosphere dichiara esattamente questi due — `--ivao-font-head: Poppins`, `--ivao-font-sans: "Nunito
Sans"` — **ma non li ha mai caricati**: nessun `@font-face` nel pacchetto. Quindi fino a oggi ogni
schermata dell'hub cadeva sul sans-serif che il computer di chi legge aveva.

- **Caricati in locale** con `@fontsource/poppins` e `@fontsource/nunito-sans`: Vite impacchetta i
  file, e il sito non chiede niente a Google né a va.ivao.aero per conto di chi lo visita. Licenza SIL
  OFL 1.1, scritta in `NOTICE`. Solo i pesi usati, divisi per alfabeto: una pagina in italiano scarica
  i file latini e basta.
- **Header e footer** seguono l'abbinamento di va.ivao.aero con le utility di Atmosphere (`font-head`,
  `font-sans`), quindi nessun file nomina un carattere.

⚠️ **Effetto collaterale da sapere, e voluto:** caricare i due caratteri li accende **ovunque**,
perché Atmosphere li usa in tutto il sistema. I titoli sono ora in Poppins e il testo in Nunito Sans
su ogni schermata — è la tipografia che Atmosphere ha sempre voluto e che mancava. Nota che è
l'abbinamento **inverso** di va.ivao.aero (lì Poppins è il testo e Nunito Sans i titoli): lo si segue
alla lettera solo in header e footer, come chiesto.

## Che cosa non è stato verificato

Tutto quanto sopra è verificato contro l'API finta degli e2e e il giro completo contro MariaDB vera,
non con un login IVAO vero. Da guardare a occhio: **la tipografia nuova sulle schermate più dense**
(le liste del back-office, l'editor), dove il cambio di carattere cambia anche le larghezze.

---

## Poi: dove porta il tasto Staff

Carmine ha chiesto che il tasto **Staff** della barra lo porti nella sua dashboard da staffista, «dove
ci sono poi tutte le sezioni che mi interessano (che vedremo più avanti)», e di verificare solo che ci
arrivi.

**Verificato in un browser, cliccando:** il tasto porta a `/staff`, che non è una pagina ma una porta —
rimanda alla **dashboard del primo dipartimento** raggiungibile (`/staff/ed` per l'utente di prova, **HQ**
per Carmine, che da superadmin li raggiunge tutti). È la dashboard del *dipartimento*, uguale per tutto
il suo staff. **Una dashboard personale da staffista non esiste**; l'unica pagina personale è `/me`, che
sta nell'area membri.

**Decisione di Carmine, 11 settembre 2026: lasciarlo così per ora.** Il tasto punta a `/staff` e non a
un dipartimento, quindi quando si progetteranno le sezioni personali basterà che `/staff` diventi quella
pagina: il tasto ci porterà senza essere toccato. Farla adesso sarebbe stata una pagina vuota, e una
schermata nuova è (c): prima la nota di design.

---

## Poi: lo spazio in cima alle schermate staff

Carmine, con due schermate cerchiate in rosso (l'editor di una news e la lista delle pagine): «ci possiamo
inventare un modo per non sprecare tutto questo spazio qui sopra?». Misurato: circa **190 px** prima del
contenuto — margine, percorso, titolo enorme, frase, altri margini — e **430** nell'editor, che aveva in
più la sua barra su una riga a parte.

Due ripetizioni lo spiegavano: il titolo era già l'ultima voce del percorso, e la frase sotto era la
stessa che la barra laterale scrive sotto la voce che porta lì.

**Scelta di Carmine fra tre proposte: una riga sola.** Percorso e titolo sono una riga, il titolo ne è la
fine, i pulsanti stanno a destra sulla stessa riga, e la riga resta in vista mentre si scorre.

- **Un componente, non trenta schermate:** `PageShell` ha due densità e la sceglie il layout, con un
  contesto (`CompactPageShells`) messo una volta in `StaffLayout`. Il sito pubblico non cambia.
- ⚠️ **`description` e `note`.** Delle descrizioni dello staff, tutte ripetevano la barra laterale
  **tranne due** che portavano un'informazione vera: quante righe sono nate da un template, e la regola
  «un divieto vince sempre» nel form dei permessi. Quelle due sono passate a `note`, che resta visibile;
  le altre restano come tooltip del titolo. Buttarle tutte avrebbe perso quelle due in silenzio.
- **La barra dell'editor sulla riga del titolo**, con `PageActions`: un portale nello slot della cornice.
  I pulsanti restano dove sta il loro stato (la cronologia di Annulla, il form che Salva invia con
  `form=`) e cambia solo dove vengono disegnati. Nell'editor le colonne partono ora a circa 140 px.
