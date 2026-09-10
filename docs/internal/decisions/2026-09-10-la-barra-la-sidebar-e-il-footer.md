# Quattro richieste di layout, e le sei rifiniture della sera

**Data:** 10 settembre 2026 — Carmine, con davanti il footer del sito della divisione UK & Ireland:

> 1. La barra di menù con Home, Getting start, pilots, ecc può essere spostata dove c'è il banner di
>    IVAO | IVAO Italy, così da risparmiare spazio.
> 2. Nella barra di menù, se logga uno staffista, deve uscire un tasto per andare alla sezione staff.
> 3. Nella sezione staff vorrei che la barra laterale abbia il tasto per chiuderla in alto e che sia
>    solo un tasto, non con tutta la scritta «Close sidebar», così da risparmiare spazio.
> 4. Il footer deve essere come quello in foto (possibilmente i link editabili dalla sezione del
>    webmaster).

**Stato:** **tutte e quattro fatte.** La 1 e la 2 subito, perché erano (a); la 3 e la 4 dopo una
decisione di Carmine, perché ognuna chiedeva qualcosa che §16.E non lascia decidere da soli — un
componente nell'elenco chiuso e una colonna di database. In fondo ci sono le **sei rifiniture** che
lui ha chiesto la sera stessa, guardando il risultato.

---

## 1 e 2 — fatte, ed erano (a)

Il menu, la ricerca, la lingua, il tema, l'account e il nuovo tasto **Staff** stanno ora tutti nello
slot `children` di `Navbar`, che Atmosphere disegna in fondo alla stessa riga del logo e del nome
della divisione. Due righe diventano una: l'intestazione del sito è alta quanto il banner.

Tre cose viste guardando, non deducendo:

- i controlli sul banner scuro vanno forzati **bianchi**, altrimenti sono testo scuro su blu scuro;
- ⚠️ **il pulsante di accesso era blu sul blu.** La variante primaria di Atmosphere è lo stesso
  `atmos` della barra, quindi l'unica chiamata all'azione del sito pubblico era un rettangolo scuro
  su un rettangolo scuro. Ora è `secondary`;
- il tasto **Staff** compare alla stessa condizione che usa la guardia della rotta,
  `isStaff || isSuperadmin`, e ci sono **tre casi di test** perché resti così. Un tasto che porta a
  `/forbidden` insegna a diffidare della barra in cui sta. Il quarto test copre il superadmin senza
  posizioni staff, che è lo stato di un'installazione nuova.

---

## 3 — La barra laterale dello staff: **decisa (strada A) e fatta**

Quello che Carmine chiede è ragionevole e piccolo. Il problema è **dove sta il pulsante**: dentro
`SidebarContainer` di Atmosphere, e non è configurabile. Letto nel bundle, non supposto:

```js
jO = () => { … <button aria-label="Toggle sidebar" className="… w-full … border-t py-3">
      <Chevron/>{ isSidebarOpen && <span>Close sidebar</span> } </button> }
MO = ({children}) => <aside …>{ <div>{children}</div> }{ <jO/> }</aside>
```

Cioè: **in fondo**, **a tutta larghezza**, con la scritta **cablata in inglese**.

⚠️ **E qui c'è un difetto nostro che nessuno aveva notato: il back-office italiano dice «Close
sidebar» in inglese.** È la regola 1 di `docs/UI-GUIDELINES.md` — nessuna stringa visibile fuori dai
file di lingua — violata da una libreria invece che da noi, il che non la rende meno vera per chi
legge lo schermo.

**Perché è (c):** `SidebarProvider`, `SidebarContainer`, `SidebarItem`, `SidebarCollapseButton` e
`SidebarContext` sono esportati, ma **`SidebarGroup` no** — solo il suo tipo. Comporre la nostra
barra dai pezzi vuol dire riscrivere il gruppo, cioè **un componente custom nuovo**, e §8.3 dice che
l'elenco è chiuso e che aggiungerne uno è una decisione esplicita, mai l'effetto collaterale di un
task. Sarebbe il **ventunesimo**.

⚠️ E c'è un precedente che consiglia prudenza, scritto nel file stesso: avvolgere `Sidebar` in un
contenitore nostro una volta ha disegnato **tutto il back-office dentro un `aside` da 288 px**, con
il pulsante di chiusura doppio (HANDOFF §13).

### Le tre strade

| | Che cosa costa | Che cosa dà |
|---|---|---|
| **A. Barra nostra** (consigliata) | Un componente nell'elenco chiuso, il gruppo riscritto, il rischio del precedente qui sopra | Il pulsante dove Carmine lo vuole, di sole icone, **e tradotto** |
| **B. Solo CSS** | Poche righe | Nasconde la scritta, ma il pulsante **resta in fondo** — e sono regole nostre appese ai nomi interni di una libreria, che è una copia locale di un meccanismo altrui (`CLAUDE.md` §2) |
| **C. Niente** | Zero | Resta com'è, «Close sidebar» compreso |

### Com'è andata

**A**, scelta da Carmine il 10 settembre 2026. `web/src/shared/ui/StaffSidebar.tsx`, ventunesimo
dell'elenco chiuso, con la sua sezione in `/staff/admin/ui-kit` e quattro test.

⚠️ **Quello che non è stato riscritto** è la metà importante: lo stato aperto/chiuso resta
`SidebarProvider` e `SidebarContext` di Atmosphere, e ogni foglia resta il loro `SidebarItem`. Nostri
sono solo la cornice e l'intestazione di gruppo, cioè **i due pezzi che la libreria non lascia
raggiungere**. Il precedente di HANDOFF §13 non si è ripetuto perché questo componente **è**
l'`aside` e non ci si avvolge attorno — e l'e2e che misura la geometria della colonna è ancora lì,
verde, con l'unica riga cambiata che è quella che cercava le parole «close sidebar» e ora chiede il
pulsante per nome.

**Due cose viste facendo:**

- ⚠️ **`useEffect` per aprire il gruppo della pagina corrente non passa il lint**, e il lint ha
  ragione: `react-hooks/set-state-in-effect`, perché il primo disegno mostrerebbe il pannello
  sbagliato. Lo stato è diventato «nessuno ha ancora detto» (`null`), e un gruppo è aperto perché la
  pagina è una delle sue finché qualcuno non clicca. Nessun effetto;
- il test non può usare `toBeVisible` sulle parole di un gruppo chiuso: le nasconde una classe
  Tailwind e jsdom non carica fogli di stile, quindi quell'asserzione passerebbe comunque. Si misura
  la larghezza del pannello e il fatto che l'elenco non sia disegnato.

---

## 4 — Il footer: **deciso (strada A) e fatto**

Il footer della foto è: marchio, una frase sulla divisione e le icone social; poi **tre colonne con
un'intestazione ciascuna** (Quick Links / For Members / Resources); poi una riga in fondo con
copyright, versione e una frase.

**La notizia buona: le tre colonne sono già esprimibili.** `cms_menu_items` ha `Scope = Footer`,
`ParentId`, `Sort`, `Label` tradotta, `Visibility` e `IsActive`, ed è **già editabile dal
back-office del dipartimento che possiede il sito** — cioè il webmaster, che è esattamente quello
che Carmine chiede. Una colonna della foto **è** una voce di primo livello con i suoi figli. Oggi il
renderer le appiattisce tutte in una riga sola: disegnarle a colonne è lavoro di layout, **(a)**.

**Quello che manca sono due cose, e sono due colonne di database:**

1. **Un'intestazione non è un link.** «QUICK LINKS» non porta da nessuna parte, ma oggi `Path` è
   obbligatorio. Serve `Path` **nullable per una voce che ha figli**, con il validatore che dice la
   regola: *un'intestazione ha figli e nessun indirizzo, una foglia ha un indirizzo*. Migrazione
   additiva. ⚠️ Tocca la decisione dell'**8 settembre** sull'insieme chiuso degli indirizzi di una
   voce di menu, che va riletta: non la contraddice — una voce che non porta da nessuna parte non
   porta neanche fuori dal sito — ma va scritto lì.
2. **Le icone social.** Discord, X, Facebook, Instagram, YouTube sono cinque link con un'icona, e una
   voce di menu non ha un'icona. Serve `Icon` nullable, presa dall'**allowlist che esiste già**
   (`web/src/shared/icons/`, il campo `.meta({ icon: true })` che i blocchi usano): allungarla è una
   riga, dice `CLAUDE.md` §4. ⚠️ Ma `lucide` non ha i marchi — niente logo Discord, X o Instagram —
   quindi quei cinque loghi vanno aggiunti a mano in `shared/icons/`, che è **il caso previsto** da
   quella stessa regola, non un'eccezione.

**Due cose restano parole e non righe**, e la raccomandazione è di lasciarle così: la **frase sulla
divisione** sotto il marchio e la **riga in fondo** («Part of the International Virtual Aviation
Organisation») vanno in `locales/`, dove sta già `footer.disclaimer`. Sono una frase per lingua, non
un elenco che qualcuno riordina, e un fork le cambia dove cambia ogni altra frase.

### Le tre strade

| | Che cosa costa | Che cosa dà |
|---|---|---|
| **A. Intero** (consigliata) | Due colonne additive, il validatore, cinque icone a mano, il layout | Il footer della foto, con i link **e le intestazioni** editabili dal webmaster |
| **B. Solo le colonne** | Nessun cambio di modello | Le tre colonne, ma un'intestazione deve puntare da qualche parte, e niente social |
| **C. Colonne dal menu, social da `division.json`** | Una colonna sola (`Path` nullable) | Come A, ma i social li cambia chi tocca la configurazione, non il webmaster — e Carmine ha chiesto il contrario |

### Com'è andata

**A**, scelta da Carmine il 10 settembre 2026. Il conto, per intero:

- **una colonna nuova**, non due: `cms_menu_items.icon`, nullable, `varchar(64)`, migrazione
  puramente additiva (`AddMenuItemIcon`);
- ⚠️ **`Path` è rimasto `NOT NULL`, e un'intestazione è la stringa vuota.** Era previsto di renderlo
  nullable; non si è fatto perché sarebbe stata l'unica modifica *a una colonna che esiste già* in
  tutta la catena, su una tabella che ogni fork ha, e §11.3 vuole migrazioni additive. Lo stato che
  `NULL` avrebbe espresso lo esprime già la stringa vuota, e **la regola sta nel validatore**, che è
  dove si legge: `IsAHeading` — in fondo al **footer**, di **primo livello**, senza indirizzo. Nel
  menu in cima e per un figlio resta obbligatorio, e le due metà hanno ognuna la propria ragione:
  un figlio senza indirizzo è una riga che non si può cliccare, un'intestazione nella barra in alto
  è una voce che premuta non fa niente;
- **il server non sa che cosa sia un'icona**: tiene il nome e non lo risolve mai, come tiene il corpo
  di una pagina senza sapere che cosa sia un blocco. L'insieme dei nomi vive solo in TypeScript, e
  una copia qui sarebbe la seconda lista da tenere allineata. Il validatore controlla **solo la
  lunghezza**;
- ⚠️ **`lucide` non ha più i marchi.** Li ha tolti tutti alla versione 1 — verificato nel pacchetto
  installato, non supposto — quindi i cinque sono disegnati a mano in `shared/icons/brands.tsx`, che
  è **il caso che le UI guidelines §2 prevedono** e la ragione per cui quella cartella esiste. Sono
  marchi semplificati sulla griglia di lucide, e la nota lo dice: chi vuole i logotipi ufficiali li
  mette nella media library;
- **una regola inferita, una sola, e va detta**: una colonna del footer i cui link portano **tutti**
  un'icona è la riga degli account, e viene disegnata sotto le parole della divisione invece che come
  quarta colonna di testo. È l'unico pezzo dedotto invece che dichiarato, e lo è perché l'alternativa
  era un secondo campo su **ogni** voce di menu per rispondere a una domanda che in tutto il sito se
  la pone una colonna sola.

**Una cosa tolta perché era diventata doppia:** `SchemaForm` teneva la propria tabella di glifi
disegnati una volta al caricamento del modulo (perché un componente letto da una mappa dentro un
render React lo rimonta, e `react-hooks/static-components` lo rifiuta). Il footer ne avrebbe voluta
una seconda, di un'altra misura. Ora è **una funzione sola**, `iconGlyph(nome, classi)`, con una
cache per insieme di classi: `CLAUDE.md` §2, applicata invece che aggirata.

**Quello che resta parole e non righe**, come raccomandato: la frase sotto il marchio
(`footer.about`) e la riga in fondo stanno in `locales/`, accanto a `footer.disclaimer`.

---

## Che cosa non è stato verificato

Le due schermate dell'intestazione sono prese in un browser vero contro l'API finta degli e2e, a
1440 px. **Con un login vero non è stato guardato**, e la cosa da guardare è il comportamento della
barra **stretta**: con più voci di menu di quelle della fixture, una riga sola può andare a capo, e
quanto succede prima dipende da quanti sono i nomi che la divisione scrive nel menu.

---

## Poi, guardandolo: tre rifiniture (10 settembre 2026, sera)

Carmine ha aperto la pagina e ha chiesto tre cose piccole. Tutte **(a)**, tutte fatte.

1. **Le voci del menu centrate.** ⚠️ Non si poteva con `Navbar`: mette i propri `children` in un
   riquadro in fondo alla riga, e un riquadro che non cresce non puo tenere niente in mezzo. La barra
   ora e composta da `NavbarContainer` e `IVAOLogo` — che sono comunque di Atmosphere — in tre zone,
   con quella centrale che cresce. Il prezzo sono otto righe: logo, diagonale e nome.
2. **La lingua e una sigla.** `EN`, `IT`. Il nome intero non sparisce, si sposta: e `aria-label`,
   quindi chi legge con uno screen reader sente ancora «English» e non due lettere. Scritto per
   esteso occupava piu spazio della ricerca, del tema e dell account messi insieme, su ogni pagina
   del sito, per dire una cosa che il lettore sa gia.
   ⚠️ Un e2e sceglieva la lingua per nome (`/italian|italiano/i`) e ora la sceglie per sigla.
3. **Il pulsante della barra laterale piu discreto.** Piu piccolo, senza riempimento, e prende colore
   solo sotto il puntatore: e un comando della cornice, non un posto dove andare.

### E poi altre tre, sempre guardando

4. **Il pulsante non aveva bisogno di una riga sua.** La fascia che occupava era vuota, e Carmine ci
   ha disegnato un cerchio sopra. Ad aperto ora galleggia nell angolo accanto alla prima
   intestazione; a fargli spazio e il padding che **ogni** intestazione porta a destra, cosi i
   chevron restano in colonna e si cede solo l angolo. A chiuso torna nel flusso: una striscia da 68
   px non ha angoli da cedere e il pulsante finirebbe sulla prima icona.
5. **La barra laterale arriva al footer.** ⚠️ La causa era `h-full`, cioe `height: 100%`: contro una
   riga di altezza `auto` si risolve nell altezza del contenuto e **annulla** lo `items-stretch`
   della riga. Toglierlo e la correzione, non una dimenticanza.
6. **Il footer ha il colore dell header.** E qui c e la cosa da sapere: dentro una fascia che porta il
   proprio sfondo i token del tema non valgono piu — `text-muted-foreground` e `Subtle` sono scuri
   su chiaro, e su quel blu sarebbero illeggibili in tema chiaro e invisibili in tema scuro. Quindi i
   colori del footer sono scritti a mano.
   ⚠️ **E questo ha rotto un test, giustamente.** `e2e/contrast.spec.ts` misura il testo secondario
   cercando `.text-muted-foreground`, e il suo commento diceva «il footer da solo ne porta quattro,
   quindi su qualunque schermata c e qualcosa»: tolti quelli, sulla home pubblica non restava niente
   da misurare e la guardia e scattata. La correzione **non** e stata indebolire il test ma seguirlo:
   i testi secondari del footer si dichiarano con `data-secondary`, il selettore ne tiene conto, e
   ora il controllo misura anche il bianco al 70 % sul blu — che passa AA.
