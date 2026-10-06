# Le regole di «Pubblica» e il tipo dell'evento nella ricerca (E3b)

**Data:** 6 ottobre 2026 — fase E3b di M4 (la vita dell'evento), PR #221
**Stato:** **decisa** (Carmine, 6 ottobre 2026, in chat al master e pubblicata sulla PR su sua istruzione: [la risposta][ok], autore
`SkyMistery`), alla domanda del revisore ([i rilievi sulla #221][r], punto 1): **sì alle tre regole** che la sessione aveva scritto
come letture del design. Il punto 5 dei rilievi (i file di una bozza) Carmine l'ha lasciato alla sessione: §3.
**Regola applicata:** `CLAUDE.md` §5 (un comportamento che il design non dice lo decide il maintainer, e la decisione sta in una
nota). Design `09-design-m4.md` §2.2, §2.4, §8.1; note `2026-09-29-la-vita-di-un-evento` §2, `2026-09-29-i-tipi-di-evento`.

[ok]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670
[r]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012258987

## 1. Che cosa serviva decidere

Il design dice che cosa controlla «Pubblica» (§2.2) e che cosa va in calendario e nella ricerca (§8.1), ma non tre cose che il
codice di E3b deve pur fare: che cosa succede a una modifica di un evento già pubblicato, se un evento con slot può uscire senza la
data in cui si prenota, e con quale tipo una riga dell'evento sta nella ricerca.

## 2. Le decisioni

1. **(a) Un evento pubblicato resta pubblicabile**: si salva solo se passerebbe ancora i controlli di «Pubblica»
   (`EventSaving.PrepareAsync`, con le chiavi di `EventPublishing`), e **l'ultimo scalo di un evento pubblicato con slot non si
   elimina**. È ciò su cui poggia l'uscita programmata (nota `la-vita-di-un-evento` §2.2): all'ora X esce la riga com'è, perché è
   già passata dai controlli, e nessun rifiuto capita a quell'ora. Come un tour pronto resta pronto (M2).
2. **(b) Un evento con slot ha bisogno di `bookingOpensAtUtc` per essere pubblicato**, anche con i soli slot privati
   (`events:errors.bookingOpensRequired`): senza quella data nessuno prenota mai (§3.3).
3. **(c) Il tipo della riga di ricerca di un evento è `events`**, la chiave del modulo: `event` è una parola seminata del calendario,
   e il modulo non ne scrive nessuna (nota `i-tipi-di-evento`; `EventsArchitectureTests`). Il distintivo della ricerca senza parola
   per il tipo (`search.kinds` in `common.json`) lo sistema il nucleo, non questa fase.

## 3. La scelta lasciata alla sessione (punto 5 dei rilievi)

**Una bozza tiene i suoi file finché è una bozza** (`Event.MediaUses`: uso senza fine); **pubblicato, l'evento li tiene fino alla
fine + 7 giorni** (design §2.4), e il conto segue la fine quando si sposta. Con la regola di prima, uguale per tutti, una bozza scritta
con una finestra già passata — per sbaglio, o per spostarla più tardi — perdeva il banner al primo giro di `media-expiry`, prima che
qualcuno la pubblicasse o ne correggesse le date. Una bozza abbandonata si elimina (nessuna riga dei membri), e i suoi usi vanno con
lei.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una modifica di un evento pubblicato senza i controlli | l'uscita programmata farebbe uscire una riga mai controllata, e il rifiuto non ha più un'ora in cui capitare |
| Un evento con slot pubblicato senza `bookingOpensAtUtc`, che apre alla pubblicazione | una data che il design tiene sull'evento (§1.2) decisa in silenzio dal codice |
| `event` come tipo della riga di ricerca | è una parola del calendario della divisione, che il codice non conosce |
| I file di una bozza fino alla fine + 7 giorni, come i tour | una bozza con le date sbagliate perde il banner prima di uscire |

## Da portare nel piano

- **Design `09-design-m4.md` §2.2** (lo porta il master): «Pubblica» si chiede anche a ogni modifica di un evento pubblicato, e
  l'ultimo scalo di un evento pubblicato con slot non si elimina; un evento con slot, anche solo privati, esce solo con
  `booking_opens_at_utc`.
- **Design §8.1**: la riga di ricerca di un evento ha il tipo `events`, la chiave del modulo; e, alla riga «Usi dei file» (come al
  §2.4), i file di una bozza restano finché è una bozza, quelli di un evento pubblicato fino alla fine + 7 giorni.
