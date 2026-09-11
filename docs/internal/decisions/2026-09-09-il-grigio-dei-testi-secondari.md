# Il grigio dei testi secondari nel tema scuro

**Data:** 9 settembre 2026 — punto 1 del giro visivo di M1 (`2026-09-07-giro-visivo-m1.md`), l'ultimo
che fosse rimasto aperto come decisione.
**Stato:** **decisa da Carmine il 9 settembre 2026 — si sovrascrive il token**, e fatta lo stesso
giorno. In fondo c'è che cosa è costata e che cosa il banco di prova ha trovato mentre la si faceva.
**Perché esiste:** è l'unico token di Atmosphere che questo hub sovrascrive, e §4 di `CLAUDE.md` dice
«Atmosphere così com'è». Una deroga a una regola scritta non si prende dentro un task.

## Che cosa non va

Atmosphere definisce i colori a coppie «sfondo / testo sopra» e nel tema scuro le ribalta tutte —
tranne una.

| token | chiaro | scuro |
|---|---|---|
| `--foreground` | fuselage-800 | fuselage-**100** |
| `--card-foreground` | fuselage-800 | fuselage-**200** |
| `--secondary-foreground` | fuselage-800 | fuselage-**100** |
| **`--muted-foreground`** | fuselage-500 | fuselage-**500** |

`#606282` è un grigio scuro: su bianco fa **5,89 : 1** e va benissimo, su `#12131b` fa **3,14 : 1**.
WCAG AA chiede **4,5 : 1** per il testo sotto i 18,66 px in grassetto o 24 px normale, e questo testo
sta a 12 e 14 px. Misurato nel browser su `/about`: i codici posizione dello staff, la nota «solo i
membri che hanno fatto login», le didascalie della striscia live (3,29 : 1, su pannello
semitrasparente), le voci del piè di pagina.

Non sembra una scelta: sembra una riga dimenticata da loro, e vale la pena segnalarla a monte.

## Le tre strade, e perché una sola regge

1. **Smettere di usare il grigio per il testo piccolo.** `text-muted-foreground` compare **72 volte
   in 25 nostri file**, ognuna una decisione di stile a sé — ma il punto che la affonda è un altro:
   **quel token lo usano anche i componenti di Atmosphere**, 24 volte dentro il loro bundle. Le
   nostre 72 le raggiungiamo, le loro 24 no.
2. **Sovrascrivere il token nel tema scuro.** Una riga, e sistema le une e le altre insieme.
3. **Non fare niente e aspettare Atmosphere.** Il sito resta sotto AA per un tempo che non decidiamo
   noi.

## Che grigio, e perché non il più chiaro

`fuselage-400` (`#8b8ca9`), **per simmetria e non per gusto**: mette il testo secondario del tema
scuro a 5,66 : 1 dal fondo pagina e 5,29 : 1 da una scheda, cioè la stessa distanza che il tema
chiaro tiene già (5,89 : 1 dal bianco). `fuselage-300` passerebbe con più margine — 7,90 : 1 — e
smetterebbe di leggersi come *secondario*: si avvicina troppo al testo principale, che nello scuro è
fuselage-100/200.

## Dove va la riga, e perché quello è il punto delicato

In fondo a `web/src/styles/index.css`, **dopo** gli import di Atmosphere. Non è pignoleria: il foglio
di Atmosphere viene caricato dopo le utility di Tailwind, e la stessa cosa ci ha già morso con
`hidden sm:block` (`UI-GUIDELINES`, «two Atmosphere quirks»). Verificato nel browser, non dedotto.

---

## Che cosa è costata

Una riga di CSS, un file di test, e tre lezioni che valevano più della riga.

- **`e2e/contrast.spec.ts`** misura ogni testo secondario visibile di cinque schermate pubbliche e
  quattro di back-office, in tema scuro, e chiede che ognuno stia sopra la sua soglia (4,5 : 1, o
  3 : 1 se WCAG lo considera testo grande). Verificato rimettendo `fuselage-500`: fallisce, e nel
  messaggio elenca i colpevoli uno per uno.
- ⚠️ **Il caso che non avevo verificato c'era davvero**, ed è stato il giro completo a dirlo: il
  grigio su un pannello `muted`. Su dodici schermate pubbliche, 109 testi, il peggiore è **5,11 : 1**
  — il numero del giorno nella griglia del calendario. Passa, perché quel pannello è
  semitrasparente e si compone sul fondo scuro invece di essere `fuselage-700` pieno. Se fosse stato
  pieno sarebbe stato 4,15 : 1, sotto soglia.
- ⚠️ **Rompendo il test è uscito un caso peggiore di quello che aveva aperto la nota**: la casella di
  ricerca del back-office, «Search, or jump to a screen», a **2,84 : 1**. Nessuno l'aveva guardata:
  il giro visivo era passato dal sito pubblico.

Tre trappole da ricordare, tutte e tre pagate qui:

1. ⚠️ **Un colore letto con una regex può essere inventato.** Atmosphere restituisce alcuni colori
   come `oklab(...)`, e prenderne i numeri con `match(/[\d.]+/g)` dà tre valori vicini a zero, cioè
   nero: la prima versione della misura dichiarava rapporti di 1 : 1 e 2,23 : 1 su tutto. I colori si
   convertono facendoli dipingere su una canvas e rileggendo il pixel.
2. ⚠️ **Un fondo semitrasparente non è il colore che dichiara.** Va composto risalendo la catena dei
   genitori, o metà delle misure sono di un colore che nessuno vede.
3. ⚠️ **Vite può servire un foglio di stile vuoto dopo che il file è stato riscritto da capo**
   (`cat >` invece di una modifica). La pagina si disegna senza stili, ogni testo risulta a 16 px e
   nero su nero, e la misura sembra catastrofica per un motivo che non c'entra niente. Si controlla
   che `--muted-foreground` sia definito prima di credere a qualunque numero — o si riavvia Vite.
