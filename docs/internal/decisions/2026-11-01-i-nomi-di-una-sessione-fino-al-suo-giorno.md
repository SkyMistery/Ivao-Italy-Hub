# I nomi di una sessione fino al suo giorno (#313)

**Data:** 1 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`, §4, privacy).
**Stato:** **decisa** (Carmine, in chat alla sessione master il 19 ottobre 2026, [risposta sulla issue #313][r1], pubblicata su
sua istruzione: «Per le sessioni di training completate si, la b»).
**Regola applicata:** piano §9.7, «Privacy dei membri»; `CLAUDE.md` §5, caso **(a)**: che cosa mostra una pagina del modulo.
Corregge la nota `2026-09-25-il-training-in-pubblico` (che non si modifica).

[r1]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/313#issuecomment-6269154103

## 1. Il problema

- La nota `il-training-in-pubblico` rende pubblico un training **programmato**: postazione, rating, data e ora a tutti, VID e nomi
  del trainee e del trainer a chi ha fatto il login.
- Nel codice `Training.SessionIsPublic` prende anche i training **completati**, di qualunque età (la voce del calendario resta per la
  sessione tenuta, A9), e `GET /api/training/sessions/{id}` dava i loro nomi a ogni membro con il login.
- Gli id sono sequenziali: chi li scorreva leggeva **tutta la storia dei training** della divisione — chi ha fatto quale rating, su
  quale postazione, quando, con chi. Né l'esito né il rapporto uscivano dal server.

## 2. Le tre strade

| | Che cosa |
|---|---|
| a | Come oggi: i nomi di ogni sessione, anche passata, a chi ha fatto il login |
| **b** | **I nomi solo fino al giorno della sessione**; dopo, la pagina resta senza persone |
| c | Nessuna pagina pubblica dopo la sessione |

**Scelta b** (Carmine).

## 3. La decisione

- **Fino al giorno della sessione compreso** — il giorno del fuso orario della divisione, lo stesso che dice quando una sessione «è
  tenuta» (design M3 §1.2) — la pagina `/training/sessions/{id}` nomina il trainee e il trainer a chi ha fatto il login, **qualunque
  sia lo stato**: anche una sessione già cominciata, o con il rapporto già pubblicato quel giorno.
- **Dopo il suo giorno** la pagina resta, ma **senza persone**: rating, postazione, data e ora, come la legge un visitatore; e non
  offre più il login «per vedere il trainee e il trainer», perché non li mostrerebbe.
- **Il trainee e lo staff** continuano a leggere i nomi delle sessioni passate dalle loro pagine (`/training/mine`, il back office):
  la regola è solo della pagina pubblica.
- La voce del calendario non cambia: non ha mai avuto nomi né VID.

## 4. Che cosa si tocca

Solo il modulo Training:

- `PublicSessions.NamesWho` (`Public/PublicSessions.cs`): la regola, una volta sola — la sessione ha una data e il suo giorno non è
  finito (`ScheduledStartUtc >= StaffQueue.HeldBefore`). I nomi non vengono nemmeno chiesti per una sessione il cui giorno è finito.
- `PublicSessionDto.Named`: un campo nuovo che dice se la sessione nomina ancora chi, anche al visitatore; la pagina offre il login
  solo allora (`screens/public.tsx`).
- L'elenco di `/training` e il blocco `training.upcomingSessions` mostrano già solo le sessioni il cui giorno non è finito: per loro
  non cambia nulla.

## Da portare nel piano

- **§9.7, «Privacy dei membri»**: VID e nomi di un training solo a chi ha fatto il login, sulla pagina della sessione, e **solo fino
  al giorno della sessione** (nel fuso orario della divisione); dopo, la pagina resta senza persone. Il trainee e lo staff li leggono
  dalle loro pagine.
- **Design M3 (`07-design-m3.md`) §4.1** e la nota `2026-09-25-il-training-in-pubblico` §2 («La pagina della sessione risponde per
  chi guarda»): la stessa precisazione.
