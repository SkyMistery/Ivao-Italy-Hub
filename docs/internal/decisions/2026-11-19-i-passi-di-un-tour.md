# I passi di un tour: che cosa il design non diceva

**Data:** 19 novembre 2026, fase 6, voce (q): le schermate per creare e modificare un tour (`11-design-pagine-dei-tour.md` §5). Prima
PR delle due: i passi; l'anteprima del §5 è la seconda, in fila.
**Stato:** **scelte della sessione di lavoro**, dentro quello che il design ha già deciso (approvato da Carmine il 31 ottobre 2026, §8,
risposta 3: «(a) un percorso a passi per il tour nuovo e le stesse schede per quello esistente»). Dicono dove e come, non che cosa; il
punto 4 sposta un tasto e il revisore lo guarda per primo.
**Classificazione** (`CLAUDE.md` §5): (a) e (b) dentro il modulo dei tour, solo il browser. Nessuna migrazione, nessun endpoint, nessun
componente nuovo (i passi sono le `Tabs` di Atmosphere con il numero davanti, come dice il §6 del design), il nucleo non cambia.

## 1. Che cosa sta in ogni passo

Il §5 nomina i sei passi e che cosa contiene il terzo; il resto è deciso così, con la regola «un campo sta dove lo si scrive»:

1. **Tipo**: un form di un campo (`tourKindSchema`), con sotto la frase di ogni tipo (`tours.kindHelp.*`), quello scelto in evidenza. Il
   tipo di un tour già pubblico non cambia più (§1.2.1 del design M2): il passo lo dice e non offre il form.
2. **Nome, date e immagini**: la parte del form del tour che prima era «Impostazioni» meno il tipo: titolo, indirizzo, riassunto, date,
   anteprima, copertina, banner, **e anche l'aereo di riferimento e l'award**. Non sono «nome, date e immagini», ma non sono regole
   (#369 le ha già separate, `TOUR_RULE_FIELDS`) né percorso: sono che cos'è il tour, e restano dove erano.
3. **Percorso**, secondo il tipo (`routeParts`): hub e rotazioni poi leg (`Hub`); leg (`Sequential`, `Free`,
   `SequentialChosenStart`, `Distance`); sottotour (`Container`); l'obiettivo (`Open`). Un modello non ha leg, hub né sottotour (§1.10):
   ha il passo solo se è `Open`, per l'obiettivo.
4. **Regole**: la metà del form che è regole (#369), le regole del tour, **i vincoli sul callsign** e, per un `Open`, **i filtri e le
   regole di sequenza**. È la lettura del §5 «Callsign e Open stanno dentro Percorso e Regole»: l'obiettivo è percorso, i vincoli sono
   regole — come la pagina pubblica, che mette callsign e vincoli in «Regole» (§4).
5. **Briefing**: com'era.
6. **Controllo**: ogni voce di `ready-problems` con «Vai a "…"» verso il suo passo, e «Segna pronto». Un modello non ha il passo
   (non si segna pronto). Un tour già pronto dice che lo è e il giorno del rilascio.

## 2. Il tipo nell'indirizzo, finché il tour non esiste

Il §5 vuole il tour bozza dal passo 2. Il passo 1 allora non salva: il tipo scelto va nell'indirizzo (`/staff/tours/new?kind=Hub`), il
passo 2 lo porta nel `POST`, e un ricarico lo ritrova. Dopo il salvataggio del passo 2 la schermata va al passo dopo
(`/staff/tours/{id}?tab=route`, o `rules` per un modello). Su un tour nuovo i passi dal 3 in poi si vedono, con il loro numero, e dicono
che si aprono dopo il passo 2.

Alternativa scartata: salvare al passo 1 una bozza con il solo tipo. Il server la rifiuterebbe (il titolo è obbligatorio già in bozza)
e il design dice «dal passo 2».

## 3. Il segno sulle schede, e le voci in cima a ogni passo

Il segno è un'icona di `lucide-react` (`CircleAlert`) accanto al nome del passo, con il testo per i lettori di schermo
(`tours.steps.toFix`). Quale passo segnare lo dice il campo della voce del server (`stepOfField`, `screens/tourSteps.ts`): il campo è
quello sotto cui `TourReadiness` e `TourShape` già archiviano il problema (`legs.3`, `hubs.LIRF`, `openGoalParameters.nm`,
`briefing.…`, `dailyLegLimit`…). Una voce di un passo che il tour non ha va al Controllo. Il riquadro «Prima di segnarlo pronto
manca» che prima stava sopra tutte le schede ora sta in cima al passo, con le sole voci di quel passo; il Controllo le ha tutte.

## 4. «Segna pronto» solo nel Controllo

La barra in alto (nascondi, torna in bozza, salva come modello, elimina) **non ha più «Segna pronto»**: il tasto sta nel passo
Controllo, dopo l'elenco di ciò che manca, come dice il §5. Due tasti uguali in una schermata sarebbero due posti dove premere senza
aver letto. «Torna in bozza» resta in alto, perché un tour pronto non ha niente da controllare.

## 5. Gli indirizzi di prima

`?tab=` prende i nomi dei passi (`kind`, `identity`, `route`, `rules`, `briefing`, `check`); i nomi delle schede di prima portano al
passo che ne contiene il contenuto (`settings` → `identity`; `legs`, `hubs`, `subtours`, `open` → `route`; `callsigns` → `rules`), così un
segnalibro non si rompe; un nome che non c'è apre il passo 2. Le pagine di una riga (un hub, una rotazione, un callsign, un vincolo, una
regola) tornano al passo che la elenca.

## 6. Avanti e indietro

Sotto ogni passo di un tour che esiste, «Passo precedente» e «Passo successivo» con il nome del passo: il percorso si segue anche su un
tour che esiste, e le schede restano per saltare. I form non avanzano da soli dopo il salvataggio (tranne il passo 2 di un tour nuovo,
che lo crea): si salva e si va avanti, due gesti, come prima fra le schede.

## Da portare nel piano

- **Design 11 §5** (e il §6, «Nessun componente nuovo»): sotto il §5 «Com'è andata» della PR dei passi; le sei scelte qui sopra, in
  particolare il tasto «Segna pronto» che lascia la barra in alto (punto 4) e callsign e vincoli in «Regole» (punto 1).
- **Design M2 §8.3** (le schermate dei tour nel back office), già sostituito dal design 11: il riferimento a «otto schede» non vale più.
