# Le righe che restano con il VID (T20b)

**Data:** 25 settembre 2026 — fase T20b di M2, tra la PR del nucleo (#119) e quella del modulo dei tour
**Stato:** **scelta tecnica**, per mantenere una decisione già presa da Carmine (nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona`,
§5 risposta 2); nessuna domanda nuova
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il meccanismo della cancellazione, appena nato, invece di aggirarlo nel
modulo. È una modifica del nucleo, quindi ha la sua PR, prima del codice del modulo che la usa.

## 1. Che cosa non andava

Carmine ha deciso che **un ban in vigore resta con il VID e il motivo**: un ban anonimo non protegge nessuno. Ma il nucleo della #119
scrive lo pseudonimo **dopo** l'eraser del modulo, in ogni colonna `vid`/`*_vid`/`*_by` di ogni tabella del modulo: anche in
`fo_bans.vid`. Il modulo non aveva nessun modo di dire «questa riga resta com'è», e il ban sarebbe diventato anonimo lo stesso. Trovato
leggendo il codice all'inizio della PR del modulo, prima di scriverlo.

## 2. Che cosa si fa

`ErasureRequest.Keep(riga)`: l'eraser del modulo passa al nucleo le righe che tiene apposta, e `PersonColumnRewrite` le salta. La riga è
l'istanza che il contesto del modulo ha già caricato: il nucleo legge con lo stesso contesto, che per l'identity resolution di EF gli
restituisce proprio quell'istanza, e il confronto è per riferimento. Nessuna chiave da trascrivere, nessun elenco di tabelle.

Una riga tenuta resta **intera**, anche nelle colonne che nominano altri (`created_by` dello staff che ha scritto il ban). Le sue righe
d'audit invece prendono lo pseudonimo come tutte le altre: nell'audit il VID non resta da nessuna parte, e il ban che protegge è la riga
viva, non la sua storia. Quando il ban scade, rilanciare la cancellazione lo anonimizza: la seconda passata non lo tiene più.

**Scartate:** un elenco di tabelle da non toccare dichiarato dal modulo (troppo largo: un ban scaduto va anonimizzato, uno in vigore no, e
stanno nella stessa tabella); una colonna del ban con un nome fuori convenzione (sarebbe l'eccezione che la convenzione esiste per evitare);
riscrivere il VID nel modulo dopo il nucleo (il modulo non gira dopo il nucleo, e rimetterebbe un dato appena tolto).

## 3. Che cosa si tocca

`Core/Privacy/IPersonalDataEraser.cs` (`ErasureRequest.Keep`, `IsKept`), `Core/Privacy/PersonColumnRewrite.cs` (salta le righe tenute),
`PersonalDataErasure.cs` (passa la richiesta). Test: il `SampleEraser` del modulo di prova tiene una riga il cui titolo lo dice, e
`ErasureTests` controlla che resti con il VID, mentre le altre prendono lo pseudonimo.

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 punto 16: una riga che un modulo tiene apposta resta con il VID; versione 1.09 e changelog.
- `HANDOFF.md`: la PR del modulo usa `request.Keep(ban)` per un ban in vigore.

Portati nella stessa PR, perché la scrive la sessione di Carmine.
