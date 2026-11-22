# Il trattino delle ore senza data

**Data:** 19 novembre 2026 (ramo `fix/hours-dash-spaced`, in coda dopo la #542).
**Stato:** decisa da Carmine il 19 novembre 2026, in chat al master: «Sì, PR a parte» ([il commento sulla
#542](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/542#issuecomment-7013404692)). Segue «Spaziato ovunque»
([#542](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/542#issuecomment-7011403735)) e la nota
`2026-11-18-la-fine-di-un-intervallo-nel-nucleo`, che aveva lasciato fuori gli intervalli senza data.
**Classificazione** (`CLAUDE.md` §5): **(a)** nella sostanza, parole dei file di lingua e due righe del modulo, più l'uso di un pezzo
del nucleo che c'è già (`intervalText`). Nessun meccanismo nuovo.

## Che cosa cambia

Anche un intervallo scritto senza data davanti — due ore nude — prende il trattino spaziato: `17:00 – 22:00`, mai più `17:00–22:00`.
Sono quattro posti, tutti degli eventi:

| Dove | Prima | Dopo | Come |
|---|---|---|---|
| La testa di un evento (`headSpan`/`compactSpan`, `cards.ts`) | `21/11/2026 · 17:00–22:00Z`, `(18:00–22:00 LT)` | `· 17:00 – 22:00Z`, `(18:00 – 22:00 LT)` | il trattino nelle due righe del modulo |
| Le ore degli slot privati (`events:private.range`) | `18:00–19:00` | `18:00 – 19:00` | il file di lingua |
| I turni delle attività (`events:public.activities.hours`) | `21/11/2099 17:00–18:30 UTC` | `21/11/2099 17:00 – 18:30 UTC` | `ActivityTurns.tsx` chiama `intervalText` del nucleo; la stringa diventa `{{when}} UTC` |
| La mail della registrazione (`events:mail.events.registrationReceived.seatLine`, `seatLinePlace`) | `Simulatore: 14/11/2026 10:00–10:30 UTC` | `… 10:00 – 10:30 UTC` | il file di lingua |

I turni delle attività scrivevano esattamente quello che scrive `intervalText` (inizio intero, fine di `intervalEnd`), con il trattino nel
file di lingua: adesso lo prendono dal nucleo, e il trattino di quella riga sta in un posto solo. La testa e gli slot scrivono due ore
nude, che nessun pezzo del nucleo scrive: lì cambia solo il trattino, senza una copia nuova.

## Fuori da questa nota

La mail della registrazione scrive la fine di un turno sempre come ora sola (`EventsMail.SeatLines`, `HH:mm`), anche quando il turno
finisce dopo mezzanotte: è il difetto del #333 punto 6, sul server. Non è il trattino: va in una issue sua.

## Da portare nel piano

- §16 punto 6: il trattino fra due ore, con o senza data davanti, è spaziato ovunque (Carmine, 19 nov 2026, i due link sopra).
- Changelog: la testa di un evento, gli slot privati, i turni delle attività e la loro mail prendono il trattino spaziato.
