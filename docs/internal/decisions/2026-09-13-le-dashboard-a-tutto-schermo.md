# Le dashboard personali, a tutto schermo

**Data:** 13 settembre 2026, sera — la nota che il piano 0.59 mette all'apertura di M2, prima di
`05-design-m2.md`
**Stato:** **decisa** (Carmine, 13 settembre 2026): §3.1–3.4 e i primi tre punti di §3.5 con domande
dirette; gli altri punti di §3.5 sono proposte che valgono finché l'uso non dice altro
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una schermata nuova (`/staff` personale) e un modo
nuovo di disporre i blocchi (la griglia a tessere). Ci si ferma, si scrive, si decide, poi si codifica.

## 1. Che cosa serve

- **`/me`**, la dashboard di un membro: «che cosa posso fare oggi» (piano §8.1) — le mie prenotazioni,
  i miei training, gli eventi a cui sono iscritto, l'ATC online.
- **`/staff`**, la dashboard personale di uno staffista: oggi è solo una porta verso la dashboard del
  primo dipartimento (`routes/_staff/staff.index.tsx`).
- **Le dashboard dei dipartimenti** (`/staff/{dept}`) esistono già dal 5 settembre come righe
  `Dashboard` disegnate come una pagina qualsiasi.

## 2. Che cosa esiste oggi, verificato

- **`/me` compone «widget»**: `IModule.Widgets` → `WidgetRegistry` → `registries.widgets` di
  `/api/me` → `MePage` disegna i componenti che il manifest del modulo registra. Il nucleo ne ha **uno
  solo**, `welcome`. Nessun modulo ne registra.
- **La dashboard di dipartimento è una riga di contenuto** (`ContentKind.Dashboard`, slug = codice del
  dipartimento, seminata da `seed/content-pages/dashboard.json`), disegnata con `ContentRenderer`
  dentro `PageShell`: titolo, descrizione, breadcrumb e sezioni larghe al massimo `max-w-7xl`.
- **Le sezioni hanno quattro larghezze** (`narrow`, `default`, `wide`, `full`) e dei layout a colonne; un
  blocco dice in quale colonna sta (`column`).
- **Il 13 settembre** la nota `moduli-non-subordinati-ai-dipartimenti` §3.4 ha deciso che i riquadri
  delle dashboard dei dipartimenti sono **blocchi Data dei moduli**, sempre `live`, che rispondono con
  ciò che chi guarda può vedere.

## 3. Le decisioni

### 3.1 Un meccanismo solo: blocchi Data (Carmine)

- **`/me` e `/staff` sono dashboard fatte di blocchi Data**, come quelle dei dipartimenti. **Il registro
  dei widget sparisce** (`IModule.Widgets`, `WidgetDescriptor`, `WidgetRegistry`,
  `registries.widgets`, i widget dei manifest): un modulo registra blocchi Data e basta, e li si mette
  dove servono — su `/me`, su `/staff`, sulla dashboard di un dipartimento, su una pagina.
- **Un blocco da dashboard risponde per chi guarda**: «le mie prenotazioni» sono le prenotazioni di chi
  apre la pagina. Il provider legge l'utente corrente e il filtro globale, come già fanno i blocchi Data;
  chi non ha niente vede lo stato vuoto del blocco («Nessuna prenotazione — vai agli eventi»), che è la
  risposta a «che cosa vede chi non ha ancora niente» del piano 0.59.
- **Le due dashboard personali sono righe `Dashboard` seminate** (slug `me` e `staff`), del dipartimento
  del sito (WD), visibilità `Members` e `Staff`: le compone il web team con l'editor, e valgono per
  tutti. Nessuna scelta per persona: una persona non riordina i riquadri (è stata l'alternativa scartata,
  due meccanismi e una tabella di preferenze).

### 3.2 Una dashboard occupa tutto lo schermo (Carmine)

- **Una dashboard non è una pagina**: non ha la colonna centrata. Si disegna su **tutta la larghezza**
  che la cornice le lascia — accanto alla barra laterale in `/staff` e `/staff/{dept}`, fra header e
  footer in `/me`.
- **Barra compatta in cima** (Carmine): una riga sola con il titolo («Ciao, Carmine», «Events») e, a
  destra, «Modifica» per chi può. Niente descrizione né breadcrumb: i riquadri partono in cima allo
  schermo.
- Vale per **tutte** le dashboard, anche quelle dei dipartimenti: una differenza fra le tre sarebbe una
  seconda regola da ricordare.

### 3.3 Una griglia a tessere libere (Carmine)

- **Il corpo di una dashboard è una griglia di tessere**, non una pila di sezioni: ogni blocco ha la sua
  **larghezza** e le tessere vanno a capo da sole. È la forma di una dashboard vera, dove la pagina a
  sezioni è la forma di un documento.
- **Nel modello**: il corpo resta un `BlockDocument` (una sezione, layout `grid`), e la larghezza sta
  nell'**envelope** del blocco, accanto a `column` e `renderMode`: `span`. Il backend la valida come
  valida `column` (un valore dell'insieme chiuso) e continua a non leggere le `props`.
- **L'editor sa disporre le tessere**: in una dashboard l'area centrale è la griglia stessa; si
  trascina una tessera per spostarla (il trascinamento c'è già, dnd-kit) e se ne cambia la larghezza.
  Palette a sinistra e proprietà a destra restano quelle di sempre.
- Sul telefono le tessere vanno una sotto l'altra, nell'ordine della griglia.

### 3.4 L'altezza: uguali per riga, e scorrono (Carmine)

- Le tessere di una stessa riga visiva hanno **la stessa altezza**, con un **massimo**: il contenuto di
  una tessera più lunga scorre dentro la tessera. La dashboard resta allineata e si legge senza scorrere
  troppo la pagina.
- Ogni blocco su una dashboard si disegna **dentro una tessera** (bordo, titolo del blocco, area che
  scorre): è la resa del blocco in quel contesto, non una proprietà che il redattore imposta.

### 3.5 I dettagli

1. **Le larghezze** (Carmine): su 12 colonne, sei valori — ¼, ⅓, ½, ⅔, ¾, intera (`span` 3, 4, 6, 8, 9,
   12). Sotto la larghezza di un tablet, ½ e meno diventano intera.
2. **Come si cambia la larghezza nell'editor** (Carmine): **tutti e due, subito** — una **maniglia** sul
   bordo destro della tessera, che si trascina e scatta sulle sei misure, per il mouse; e un
   **selettore** nel pannello delle proprietà, per la tastiera e per chi preferisce scegliere. La maniglia
   è un pezzo nuovo dell'editor: si prova nel giro e2e, non solo negli smoke.
3. **L'altezza massima** di una riga: il 40% dell'altezza della finestra, con un minimo sensato; una
   tessera che ne chiede di più scorre.
4. **I blocchi del nucleo per `/staff`** (le quattro cose scelte da Carmine):
   - «**Cose che aspettano me**» — un blocco per ciascuna: *pagine da approvare* (per chi approva),
     *contatti arrivati* ai miei dipartimenti, *documenti da rivedere* (scaduta la data di revisione);
   - «**Le mie bozze**» — le pagine, news e documenti che ho scritto e non sono pubblicati, e quelli
     rimandati indietro;
   - «**Calendario interno**» — il blocco `calendar` c'è; gli si aggiunge «i miei dipartimenti» come
     valore di `department`;
   - «**I miei dipartimenti**» — i link alle loro dashboard;
   - i **riquadri dei moduli** arriveranno con i moduli (eventi della settimana senza ATC, richieste
     training, PIREP da validare).
5. **`/me` all'inizio** (Carmine): **solo il saluto** (oggi il widget `welcome`, domani un blocco
   `welcome`). Il resto lo portano i moduli, e il web team può aggiungere dalla palette quello che c'è
   già (l'ATC online, il calendario).
6. **`/staff/{dept}` resta**: la barra laterale porta alle dashboard dei dipartimenti come oggi, e
   `/staff` smette di reindirizzare e diventa la dashboard personale.

## 4. Che cosa si tocca

- **Piano**: §8.1 (la dashboard come home), §8.2 (`/me`, `/staff`), §9.7 (niente più widget: i moduli
  registrano blocchi Data), §13 (M2), e il changelog.
- **Codice, nelle fasi della parte B di `06-piano-implementazione-m2.md`** (prima del modulo Events):
  - la **resa a tutto schermo** e la barra compatta per `ContentKind.Dashboard`;
  - la **griglia a tessere**: layout `grid`, `span` nell'envelope (walker, validazione, TypeScript),
    la resa a tessera con altezza per riga, l'editor che dispone e ridimensiona;
  - le dashboard **`me` e `staff` seminate**, `/staff` che smette di reindirizzare, `/me` che legge la
    riga;
  - i **blocchi del nucleo** di §3.5.4 e §3.5.5;
  - **via il registro dei widget**, in tutti e due i lati, con la sua voce nella galleria `ui-kit`.
- **Le dashboard dei dipartimenti già seminate** passano alla griglia: il template e le righe esistenti
  si convertono (una sezione, blocchi in tessere intere) con il seeder, perché il sito non è online.
