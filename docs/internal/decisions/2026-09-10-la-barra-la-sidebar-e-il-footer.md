# Quattro richieste di layout: due fatte, due che vogliono una decisione

**Data:** 10 settembre 2026 — Carmine, con davanti il footer del sito della divisione UK & Ireland:

> 1. La barra di menù con Home, Getting start, pilots, ecc può essere spostata dove c'è il banner di
>    IVAO | IVAO Italy, così da risparmiare spazio.
> 2. Nella barra di menù, se logga uno staffista, deve uscire un tasto per andare alla sezione staff.
> 3. Nella sezione staff vorrei che la barra laterale abbia il tasto per chiuderla in alto e che sia
>    solo un tasto, non con tutta la scritta «Close sidebar», così da risparmiare spazio.
> 4. Il footer deve essere come quello in foto (possibilmente i link editabili dalla sezione del
>    webmaster).

**Stato:** la **1 e la 2 sono fatte**. La **3 e la 4 aspettano una decisione**, ognuna per una ragione
precisa e non perché siano grosse.

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

## 3 — La barra laterale dello staff: **è (c), e per una ragione sola**

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

---

## 4 — Il footer: **il grosso c'è già, mancano due colonne**

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

---

## Che cosa non è stato verificato

Le due schermate dell'intestazione sono prese in un browser vero contro l'API finta degli e2e, a
1440 px. **Con un login vero non è stato guardato**, e la cosa da guardare è il comportamento della
barra **stretta**: con più voci di menu di quelle della fixture, una riga sola può andare a capo, e
quanto succede prima dipende da quanti sono i nomi che la divisione scrive nel menu.
