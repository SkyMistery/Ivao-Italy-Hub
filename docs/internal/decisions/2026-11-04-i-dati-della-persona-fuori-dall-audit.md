# I dati della persona fuori dall'audit (#326, elemento 2)

**Data:** 4 novembre 2026, fase di test (nota `2026-10-18-la-fase-di-test`), branch `fix/review-lot5b-audit`.
**Stato:** **la direzione della revisione** (nota `2026-10-20-la-revisione-del-codice`, lotto 5, issue #326 elemento 2: «`[NotAudited]` on
those columns»), applicata; nessuna domanda aperta per il maintainer. Scritta perché estende il significato di un meccanismo del nucleo
(`CLAUDE.md` §0, regola 6, caso **(b)**).

## Che cosa serve

`HubUser` è `[Audited]`, e l'interceptor copiava nell'audit ogni colonna tranne il token di concorrenza: `Email`, `DiscordId` e
`SecurityStamp` stavano in `before_json`/`after_json`, serviti da `GET /api/admin/audit/{id}` a chiunque abbia `Audit.View`. La nota
`2026-09-06-indirizzo-di-un-destinatario` §1 dice che nessun DTO porta l'indirizzo: l'audit era la strada che lo portava fuori. E
`LastLoginAt`, che cambia a ogni accesso, scriveva una riga d'audit per ogni accesso.

## La decisione

- **`[NotAudited]` vuol dire anche «l'audit non la copia»**: l'interceptor (`HubSaveChangesInterceptor.Serialize`) salta una colonna
  segnata così nel prima e nel dopo di ogni riga, creata, cambiata o cancellata. Fin qui voleva dire solo «un salvataggio che cambia solo
  queste colonne non è un cambiamento della riga»; resta vero.
- **Su `HubUser`**: `Email`, `DiscordId` e `SecurityStamp` (dati della persona e un segreto della sessione: il cambiamento che lo rinnova
  — un grant, il ruolo di superadmin — è nell'audit da sé) e `LastLoginAt` (contabilità: un accesso che non cambia altro non lascia righe).
- Le colonne già segnate (`PersonalToken.LastUsedAt`, `UserStaffPosition.SyncedAt` e quelle dei moduli) escono anch'esse dalle copie:
  erano contabilità, e nessuno le leggeva nell'audit.

## Che cosa non fa

**Le righe già scritte in `hub_audit_log` tengono gli indirizzi di prima.** Svuotarle è un passo sui dati di produzione, come quello che
la cancellazione fa per una persona (`AuditRedaction`); non è in questa PR. Se serve, è una migrazione di dati che va decisa a parte.

## Alternative scartate

- Un attributo nuovo (`[NotInAudit]`) accanto a `[NotAudited]`: due segni per la stessa domanda «l'audit la guarda?», e un salvataggio
  del solo indirizzo resterebbe un cambiamento della riga senza niente da mostrare.
- Togliere `[Audited]` da `HubUser`: si perderebbe la traccia di chi diventa superadmin e di chi lo toglie.

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 (il meccanismo dell'audit) e §9.7 («Privacy dei membri»): `[NotAudited]` tiene una colonna fuori dalle
  copie dell'audit; l'indirizzo, il Discord e il timbro di sessione di un membro non ci sono più dal 4 novembre 2026 (le righe di prima li
  tengono); versione e changelog.
