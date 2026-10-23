# Chi può si prende la issue, e un terzo tester

**Data:** 23 ottobre 2026
**Stato:** **decisa da Carmine in chat al master**, 23 ottobre 2026. La forma del controllo `core-guard` (§3) è del master; la PR che la
porta tocca `.github/` e `CLAUDE.md`, quindi la unisce Carmine, e unirla è la sua approvazione.
**Regola applicata:** la fase di test (`2026-10-18-la-fase-di-test`) e i ruoli di `CLAUDE.md` §0. Questa nota **sostituisce** una
regola di quella nota (§2 punto 1 e la colonna «Corregge» del §3), che resta scritta com'era.

## 1. Che cosa serve

Due cose, dette da Carmine lo stesso giorno:

- **Un terzo tester**: **`gabbosonc31`**, staff della divisione (FOD), già collaboratore del repository con il ruolo Write, con la sua
  sessione di Claude Code. Prova **il sito e i Tour**, e scrive correzioni.
- **«Ognuno corregge il suo codice» non regge più**: «lasciamo perdere che ognuno corregge il suo codice, altrimenti il lavoro diventa
  lunghissimo. Chi può si prende la issue e chiude.»

## 2. Che cosa ha deciso Carmine

1. **Chi può si prende la issue e la chiude.** Un difetto non aspetta chi ha scritto quel codice: lo corregge chi è libero. Per non
   lavorare in due sulla stessa cosa, **ci si assegna la issue prima di cominciare** (e lo si dice sulla issue), e non si toccano i
   file di una PR aperta di un altro. Una PR per difetto, ramo `fix/<slug>`, **«Closes #N»** nel corpo.
2. **Chi prova resta diviso**, perché chi ha scritto un pezzo non ne vede i buchi: **Carmine** Eventi e Training; **`dalberone`** il
   nucleo, il sito e il CMS, i Tour, il back office; **`gabbosonc31`** il sito e il CMS e i Tour; il master le prove automatiche, la
   velocità, la sicurezza, il telefono e l'accessibilità.
3. **`gabbosonc31` è un contributore** come `dalberone` (`CLAUDE.md` §0): valgono per lui e per le sue sessioni tutte le regole
   assolute del collaboratore — mai `main`, mai un merge, mai un tag, mai i file solo del maintainer; il master rivede ogni PR e Carmine
   la unisce.
4. **Le issue già assegnate restano a chi le ha** finché non le lascia: #311–#314 a `dalberone`, che può lasciarne una scrivendolo
   sulla issue.

## 3. La forma nel controllo (`core-guard`)

Prima di questa nota il controllo rifiutava a un contributore **i Tour** (il codice di un altro modulo) e **il nucleo senza una nota
nuova**, anche per un difetto: con la regola del §2 punto 1 avrebbe fermato proprio le correzioni che si vogliono.

Ora una PR di un contributore che **nel corpo chiude una issue** («Closes #N», «Fixes #N» o «Resolves #N»):

- può toccare **il codice dei Tour** (`src/IvaoHub.Modules.FlightOps/`, `web/src/modules/flightops/`, `locales/*/flightops.json`), che
  conta come nucleo invece che come file solo del maintainer;
- può toccare **il nucleo senza una nota nuova**: il controllo lo segna con ⚠️ e nomina le issue chiuse, perché chi rivede lo legga per
  primo.

Non cambia niente del resto: senza una issue chiusa il nucleo chiede ancora la sua nota e i Tour restano fuori; i file solo del
maintainer (le regole, il piano, `HANDOFF.md`, la pipeline, i test dell'architettura, le note già unite) restano rifiutati sempre. Una
correzione che rivela una scelta porta comunque la sua nota (caso (c) di `CLAUDE.md` §5, la domanda a Carmine). Il controllo resta un
segnale per chi rivede, non la serratura: la serratura è che solo Carmine unisce.

## 4. Le alternative

- **Una nota breve per ogni correzione del nucleo**, senza toccare il controllo: funziona per il nucleo ma non per i Tour, e moltiplica
  le note che non decidono niente. Scartata.
- **Un'eccezione per nome** (solo `gabbosonc31` sui Tour): una regola che invecchia appena arriva un quarto tester. Scartata.
- **Fatta**: la eccezione legata alla issue chiusa (§3).

## Da portare nel piano

- **§13**: la fase di test con chi prova che cosa (§2 punto 2) e «chi può si prende la issue» (§2 punto 1), al posto di «ognuno corregge
  il suo codice».
- **`CLAUDE.md` §0** (fatto con questa nota): `gabbosonc31` nella riga del contributore; la regola 2 dice l'eccezione della issue
  chiusa.
- **`CONTRIBUTING.md`** («Reporting a bug or a security weakness»): una riga su come ci si prende una issue (con il sì di Carmine).
