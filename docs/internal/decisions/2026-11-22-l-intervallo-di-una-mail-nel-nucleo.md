# L'intervallo di una mail nel nucleo

**Data:** 22 novembre 2026 (ramo `fix/seat-lines-end`, issue #548).
**Stato:** classificazione e forma decise dal master il 22 novembre 2026, in chat alla sessione di lavoro, sulla proposta di questa
sessione. Non c'è un commento su GitHub da linkare: la risposta è nella chat del master. Il trattino spaziato è di Carmine
(«Spaziato ovunque», [#542](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/542#issuecomment-7011403735)).
**Classificazione** (`CLAUDE.md` §5): **(b)**. Il meccanismo c'è già, `HubDates` per il server e `intervalText` per il browser
(§16 punto 6). Lo si estende con il pezzo che due moduli scrivevano ciascuno per conto suo e che un terzo posto stava per copiare.

## 1. Che cosa serve

La mail d'iscrizione a un evento scrive i turni delle attività (`EventsMail.SeatLines`). La fine la scriveva sempre come ora sola
(`HH:mm`), quindi un turno 23:30 + 60 minuti si leggeva «23:30 – 00:30 UTC», senza nessun segno che finisce il giorno dopo (#548). È
il #333 punto 6 sul server: la pagina lo scrive giusto dalla #542.

## 2. Perché nel nucleo

Sul server la stessa regola era già scritta due volte, identica riga per riga: l'inizio intero, il trattino spaziato, la fine come ora
sola se è lo stesso giorno UTC e intera altrimenti.

| Copia | Dove |
|---|---|
| `EventsMail.Shift` | i turni dei controllori (`src/IvaoHub.Modules.Events/EventsMail.cs`), con il callsign davanti |
| `TrainingMail.Span` | le date di un training (`src/IvaoHub.Modules.Training/TrainingMail.cs`) |

Correggere `SeatLines` con la regola di `Shift` dentro `EventsMail`, cioè l'opzione (a), avrebbe lasciato due copie, una negli Eventi e
una nel Training. Due moduli non condividono codice se non attraverso il nucleo, e il gemello del browser sta già nel nucleo.

## 3. Che cosa entra nel nucleo

- **`HubDates.Interval(from, to, order)`** (`src/IvaoHub.Core/Division/HubDates.cs`), accanto a `Moment`. Scrive
  `21/11/2026 17:00 – 22:00` e `21/11/2026 23:30 – 22/11/2026 00:30`. È il gemello server di `intervalText`. Una fine alle 00:00
  esatte è il giorno dopo e porta la sua data, come `intervalEnd` nel browser. I giorni si confrontano come si scrivono i due momenti,
  cioè in UTC per una mail: la notte del cambio dell'ora un intervallo di un giorno resta di un giorno.
- **I tre chiamanti lo usano**:
  - `EventsMail.Shift` diventa `{callsign} {Interval}`;
  - `TrainingMail.Span` lo inoltra e resta solo come nome del modulo, per chi lo chiama (`TrainingDates`) e per il suo test;
  - `SeatLines` lo usa al posto della fine `HH:mm` scritta a mano.

  Nessuna copia della regola resta fuori dal nucleo: il grep di `HH:mm` in `src/` trova solo `HubDates` e le ore nude dei tour
  (`PlanChecks`, `TrackChecks`), che non sono intervalli.
- **Le parole.** `events:mail.events.registrationReceived.seatLine` e `seatLinePlace` passano da `{{from}} – {{to}} UTC` a
  `{{when}} UTC`, in `en` e in `it`, sia nella radice (`locales/`) sia nel modulo (`web/src/modules/events/locales/`). È lo stesso
  passo che la #547 ha fatto con `events:public.activities.hours`: il trattino sta in un posto solo, il nucleo.

**Che cosa vede l'utente:** nella mail d'iscrizione, un turno che finisce dopo mezzanotte (o a mezzanotte esatta) porta la data della
fine: `14/11/2026 23:30 – 15/11/2026 00:30 UTC`. Le altre mail (turni dei controllori, date del training) non cambiano.

**La forma della PR.** Una sola PR, con `Closes #548`, e non due (prima il nucleo, poi la correzione in coda) come chiederebbe la regola
6 di `CLAUDE.md` §0. L'ha scelta il master, con questo motivo: il lato dei moduli toglie tre copie e chiama il nucleo, e niente altro.
Il revisore lo legge insieme al pezzo del nucleo senza confondersi. È la stessa forma della #542.

## 4. Alternative scartate

- **(a), la regola di `Shift` estratta dentro `EventsMail`**: due copie al posto di tre, ma sempre due, in due moduli.
- **Togliere `TrainingMail.Span`**, come la #542 ha tolto le `spanText`: si può fare dopo. Qui il compito era che i tre chiamanti
  passassero dal nucleo. `Span` resta un inoltro di una riga, senza una regola sua.

## Da portare nel piano

- §16 punto 6: sul server `HubDates.Interval` è l'unico posto che scrive un intervallo, gemello di `intervalText`. Lo chiamano la mail
  d'iscrizione a un evento (`SeatLines`), i turni dei controllori (`EventsMail.Shift`) e le date del training (`TrainingMail.Span`). Al
  posto della riga «Sul server la mail del Training (`TrainingMail.Span`) scrive le date allo stesso modo».
- Changelog: la mail d'iscrizione a un evento scrive la data della fine di un turno che finisce il giorno dopo (#548); le due copie
  del server (`EventsMail.Shift`, `TrainingMail.Span`) passano dal nucleo.
