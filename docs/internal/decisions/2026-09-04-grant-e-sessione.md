# Un grant invalida la sessione attraverso l'entità, non attraverso un gancio di `MapCrud`

**Data:** 4 settembre 2026 — F8
**Stato:** **decisa** (Carmine, 4 set 2026)

## Cosa serviva

F8 task 3 chiede che scrivere un grant rigeneri lo `security_stamp` del VID toccato e invalidi la
cache (`ISecurityStampCache.Invalidate(vid)`), così che il permesso morda **subito** e non al
login successivo. L'accettazione della fase lo dice esplicitamente: «grant `Links.Edit` su FOD a
uno staff ED → può modificare i link FOD subito (stamp), rimozione → 403».

`/api/admin/grants` è `MapCrud` in modalità globale (task 3 di nuovo), e il motore ha **un solo**
gancio, `ExtraWritePolicy`, che sta *prima* della scrittura. Non c'è nessun punto «dopo».

## Le tre uscite

1. **Un secondo gancio sul motore**, `AfterWrite`. Piccolo ed esplicito nel file degli endpoint,
   ma morde solo se il grant passa da `MapCrud`: un seeder, uno script di migrazione o un servizio
   futuro che scrivesse un grant a mano non invaliderebbe niente, e nessuno se ne accorgerebbe
   finché qualcuno non si ritrova con un permesso che non ha più.
2. **Un'interfaccia sull'entità**, applicata dall'interceptor — la forma di `IAuditable`,
   `IProjectable`, `IOwnedByDepartment`. Vale per chiunque scriva la riga.
3. **Non farlo in M0**, chiudendo F8 senza quella parte.

## Cosa si è fatto

Opzione 2. `Division/DomainContracts.cs` dichiara:

```csharp
public interface IAffectsUserSession
{
    int AffectedVid { get; }
}
```

`UserGrant` la implementa (`AffectedVid => Vid`), e `HubSaveChangesInterceptor` fa due cose nel
**secondo tempo**, quello che scrive audit e proiezioni:

- prima del `SaveChanges` interno, dà un `SecurityStamp` nuovo a ogni VID raccolto — quindi
  **dentro la stessa transazione della scrittura**: un rollback se lo riporta indietro, e uno stamp
  sopravvissuto a un grant fallito sloggherebbe l'utente da tutti i suoi dispositivi per niente;
- **dopo il commit**, e solo dopo, toglie la voce dalla cache. Toglierla prima inviterebbe la
  richiesta successiva a rileggere la riga vecchia e a rimetterla in cache.

Un contesto di modulo che non ha `hub_users` nel proprio modello non fa niente, esattamente come
non scrive proiezioni se non ha le tabelle: stessa domanda, stessa risposta.

### Un dettaglio di container che vale la pena scrivere

L'interceptor **non può** farsi iniettare `ISecurityStampCache`: quella legge attraverso
`HubDbContext`, che è costruito con l'interceptor dentro, e chiederla lì significherebbe chiedere
al container di costruire un contesto per costruire un contesto. Quindi l'interceptor prende
`IMemoryCache` e chiama `SecurityStampCache.Forget(cache, vid)`, che è statica; il metodo di
istanza chiama la stessa. La chiave resta scritta in un posto solo, che è l'unica cosa che conta.

## Cosa significa davvero «subito»

Il cookie vecchio viene **rifiutato** alla richiesta successiva, non riscritto: `OnValidatePrincipal`
confronta lo stamp e rigetta (design M0 §3.3, deciso in F2). Quindi la sequenza vera è: grant → la
richiesta dopo prende **401** → il browser rifà il login, che con IVAO è silenzioso per chi ha già
dato il consenso → la richiesta dopo ancora ha i permessi nuovi.

È quello che il test `AGrantReachesTheNextRequestAndItsRemovalTheOneAfter` asserisce, ed è più
onesto della lettura ottimistica: la sessione vecchia smette di funzionare all'istante, che è la
proprietà di sicurezza che serve. Ricostruire il principal invece di rigettarlo sarebbe più comodo
e cambierebbe una decisione di F2, quindi non è stato fatto in F8.

## Cosa si tocca

- `src/IvaoHub.Core/Division/DomainContracts.cs` — l'interfaccia.
- `src/IvaoHub.Core/Data/HubSaveChangesInterceptor.cs` — raccolta e applicazione, secondo tempo.
- `src/IvaoHub.Core/Auth/UserGrant.cs` — la implementa.
- `src/IvaoHub.Core/Auth/SecurityStampCache.cs` — `Forget` statica, chiamata anche da `Invalidate`.
- `docs/internal/01-design-m0.md` §3.3 e §3.4 — l'elenco di ciò che l'interceptor fa.

## Decisa

Carmine, 4 settembre 2026, con le tre uscite qui sopra messe davanti. La ragione della scelta è che
la spina dorsale «non si bypassa nemmeno dimenticando la policy» (design §9 punto 5): un gancio sul
motore CRUD è dimenticabile, un'interfaccia sull'entità no.
