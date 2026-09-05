# L'ambiente `E2E`: un modo di diventare staff senza dimostrare niente

**Data:** 5 settembre 2026 — fase G0 di M1
**Chi decide:** Carmine
**Dove vive:** `03-design-m1.md` §11.1, `04-piano-implementazione-m1.md` G0

## Cosa serve

Il giro che M1 deve provare — crea da template, aggiungi blocchi, pubblica, apri `/{slug}` da
anonimo — richiede una **sessione staff** dentro un browser, in CI, in modo riproducibile. Un login
IVAO vero non lo è: dipende da un identity provider esterno, da credenziali che non stanno nel
repository e da una persona reale.

## Perché nessun meccanismo esistente basta

`TestSignInStartupFilter` fa esattamente questo, ma vive in `tests/IvaoHub.IntegrationTests` e viene
iniettato da `WebApplicationFactory.ConfigureTestServices`: esiste solo dentro un test host in
processo. Il banco e2e ha bisogno di un **processo pubblicato**, avviato da fuori, che un browser
possa aprire. Non è un meccanismo che si estende: è lo stesso meccanismo che deve esistere anche nel
binario, e quindi è codice nuovo in `IvaoHub.Web`.

## La decisione

`POST /e2e/signin` esiste **solo** quando valgono due cose insieme:

1. `ASPNETCORE_ENVIRONMENT=E2E` (`HubEnvironments.E2E`, una costante sola);
2. `E2E:Enabled=true` nella configurazione.

Chi firma è configurato (`E2E:Vid`, `E2E:FirstName`, `E2E:LastName`, `E2E:Positions`) e passa dal
**vero** `UserSyncService`: la riga di `hub_users`, le posizioni interpretate da `StaffRoleMap`, i
permessi effettivi dalla matrice, il cookie applicativo con il suo security stamp. Quello che è
finto è l'identity provider, e nient'altro — tutto ciò che il giro esercita dopo è il prodotto vero.

**Il recinto ha due metà, e la seconda è quella che serve davvero.** L'ambiente è un nome: lo si può
sbagliare. Il flag `E2E:Enabled` in un ambiente che non è `E2E` **ferma l'applicazione** invece di
essere ignorato in silenzio (`HubConfiguration.RequireE2EEnvironment`): il modo realistico in cui
una cosa così arriva su un server è un file di configurazione copiato, e un file copiato che viene
ignorato resta lì per sempre senza che nessuno lo sappia. Test: `E2EBenchTests`.

Di rimbalzo, `FixtureIvaoApiClient` accetta ora anche l'ambiente `E2E` oltre a `Development`. Il
motivo è lo stesso per cui lo accetta in sviluppo, e più forte: il banco gira **senza credenziali
IVAO**, e il sync dei dati `ref_` è atteso prima che la prima richiesta venga servita — senza le
fixture spenderebbe quel tempo a fallire contro un'API che non può raggiungere. La regola che conta
(«un sito di produzione non serve mai spazio aereo inventato») è intatta.

## Cosa si è scartato

- **Un ambiente `Development` più un flag**: avrebbe messo il bypass a un solo booleano di distanza
  dall'ambiente in cui lavora tutti i giorni chi sviluppa.
- **Un login IVAO vero in CI**: non riproducibile, e richiederebbe credenziali nel CI.
- **Servire la SPA con un server statico e proxare l'API**: è precisamente il banco di prova che in
  M0 ha prodotto quattro test rossi contro un pacchetto sano (HANDOFF, «Il tag»). Il banco è
  l'applicazione pubblicata, che serve la SPA con il proprio fallback.
