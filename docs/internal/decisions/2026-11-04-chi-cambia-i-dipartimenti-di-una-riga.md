# Chi cambia i dipartimenti di una riga (#319, elemento 2)

**Data:** 4 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/review-lot1b-guard`.
**Stato:** **decisa** — **Carmine in chat al master, 21 ottobre 2026** («Tutte come suggerisci tu»), pubblicato su sua istruzione
sull'issue ([la risposta][a319]); e su chi «ha» il dipartimento base, **Carmine in chat al master, 4 novembre 2026** («No, come consigli»),
pubblicato sulla PR ([la risposta][b447]).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si estende il guardiano delle scritture (`HubSaveChangesInterceptor`), un meccanismo
del nucleo.

[a319]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/319#issuecomment-6310356507
[b447]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/447#issuecomment-6631319523

## Che cosa serve

Una riga di un modulo è in cura a un insieme di dipartimenti («curata da», `IOwnedByDepartment` esteso a un insieme, nota
`2026-09-13-moduli-non-subordinati-ai-dipartimenti` §3.3): il dipartimento base del modulo (ED per gli eventi) e chi collabora. Il
guardiano chiedeva il permesso su **un** dipartimento della riga prima della scrittura e su **uno** dopo: un dipartimento che collabora
poteva togliere un altro dipartimento, o aggiungerne uno qualsiasi. La nota §3.3 dice il contrario: «il SOD non tocca gli eventi degli
altri».

## La decisione

- **Chi ha il permesso sul dipartimento base della riga** (il suo `OwnerDepartment` com'era caricato) cambia l'insieme liberamente.
- **«Avere» il dipartimento base vuol dire avere il permesso su di esso intero** ([la risposta del 4 novembre][b447]): senza lo scope di
  una riga, senza un FIR, e non «da fuori». Chi lo ha solo così lavora sulla riga, ma ne cambia l'insieme come un collaboratore:
  - **un grant su una riga sola** (`resource_scope`) non dà il dipartimento (`CLAUDE.md` §2, «a grant on one row gives no department»);
  - **un grant da fuori** — a una posizione di un altro dipartimento, o di un permesso globale concedibile — dà il permesso e non il
    dipartimento (piano 1.28, `EffectivePermission.FromOutside`): conta come collaboratore;
  - **la squadra di un FIR** tiene il permesso sulle righe del suo FIR (piano 1.24, il FIR della riga come coordinata in più): conta come
    collaboratore anche lei. Lo staff del dipartimento non è mai limitato dal FIR, e il suo permesso, senza FIR, conta come base.
- **Un dipartimento che collabora** aggiunge o toglie solo dipartimenti su cui ha il permesso anche lui: ogni dipartimento aggiunto con
  lo scope e il FIR della riga com'è scritta, ogni dipartimento tolto con quelli di com'era.
- Restano le regole di prima: il permesso su uno dei dipartimenti dopo e su uno di prima, e chi crea una riga ci mette un dipartimento su
  cui ha il permesso.

Nel codice: `RequireOnEveryDepartmentChanged` nel guardiano, chiamato quando una modifica sposta la riga. L'endpoint generato non cambia:
il suo controllo prima e dopo resta, e il guardiano, al salvataggio, risponde 403 come fa per ogni scrittura che non regge.

## Alternative scartate

- Lasciare la regola di un dipartimento prima e uno dopo: è quella che la revisione ha trovato.
- Il permesso su tutti i dipartimenti della riga, prima e dopo, anche per il dipartimento base: il base non potrebbe più togliere un
  collaboratore che non c'entra più.

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 (il guardiano delle scritture, «spostare una riga fra dipartimenti») e §8 (i moduli non appartengono
  ai dipartimenti, l'insieme «curata da»): chi cambia l'insieme, come sopra; versione e changelog.
- `CLAUDE.md` §2, la riga «Department ownership…» resta com'è: è la stessa interfaccia e lo stesso guardiano.
