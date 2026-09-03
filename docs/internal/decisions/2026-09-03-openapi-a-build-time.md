# L'OpenAPI a build-time avvia l'app per un istante: come l'abbiamo reso innocuo

**Data:** 3 settembre 2026 — fase F5
**Stato:** **decisa in corso d'opera, da confermare**. Se confermata, `01-design-m0.md` §7.4 e
§9 punto 12 vanno riformulate (vedi in fondo).

## Il design

§7.4: «Il documento OpenAPI si genera **a build-time**, senza avviare l'app (che senza
`ivao-oauth.json` e DB non parte): `Microsoft.Extensions.ApiDescription.Server` in
`IvaoHub.Web.csproj`». §9 punto 12 ripete: «OpenAPI generato a build-time
(`Microsoft.Extensions.ApiDescription.Server`), **non da un'app in esecuzione**».

## Cosa fa davvero quel pacchetto

Misurato il 3 settembre 2026. Il target lancia `dotnet-getdocument`, che a sua volta esegue
`GetDocument.Insider`; questo **invoca il nostro `Program.Main` per riflessione** e lo lascia
arrivare fino a `app.Run()`, perché è lì che gli endpoint minimal API esistono: sono registrati
*dopo* `builder.Build()`, e un tool che si fermasse alla costruzione dell'host li perderebbe tutti.

La prova sperimentale è secca: con la nostra prima versione, che saltava `app.Run()` durante la
generazione, il documento uscì con `"paths": { }`. Lasciando eseguire `app.Run()`, uscì con tutti e
sei i path.

Quindi la frase «senza avviare l'app» è **falsa** per come funziona lo strumento. Ciò che è vero, e
che conta, è l'altra metà: **senza database e senza client OAuth**.

## Il problema concreto

Facendo partire l'host durante la build, tre cose della nostra sequenza di avvio hanno morso:

1. `ASPNETCORE_ENVIRONMENT` non è impostata nel processo del tool, quindi l'ambiente è
   **Production** e `RequireAllowedHosts` faceva fallire la build.
2. `IvaoOAuthOptions` con `ValidateOnStart()` faceva fallire la build su una macchina senza
   `config/ivao-oauth.json` — cioè su ogni fork e su ogni runner di CI.
3. `InitializeAsync` (migrazioni, bootstrap superadmin, sync `ref_`) avrebbe voluto un database.

## La decisione

Un flag unico, `HubConfiguration.IsOpenApiDocumentGeneration`, che riconosce il processo dal nome
dell'assembly d'ingresso (`GetDocument.Insider`), e tre punti che lo leggono in `Program.cs`:

- non si applica l'irrigidimento di Production (`AllowedHosts`);
- non si registra `ValidateOnStart()` per l'OAuth — l'app vera continua a non partire senza client;
- non si esegue `InitializeAsync`, quindi **niente database**;
- in più, `UseUrls("http://127.0.0.1:0")`: il tool apre una porta effimera sul loopback, così una
  build non litiga con un `dotnet run` già in ascolto sulla 5000.

Il riconoscimento per nome dell'assembly d'ingresso è volutamente **specifico**: sotto
`WebApplicationFactory` l'assembly d'ingresso è l'host dei test, quindi il flag resta falso e i test
di integrazione continuano a migrare, bootstrappare e servire per davvero.

Un secondo dettaglio misurato: il tool deve **eseguire** l'assembly compilato, cosa che un
`dotnet publish -r linux-x64 --self-contained` lanciato da Windows non può fare. Il documento è
comunque un artefatto di build, non di pubblicazione, quindi `IvaoHub.Web.csproj` spegne la
generazione quando c'è un `RuntimeIdentifier`.

## Alternative scartate

- **Impostare `ASPNETCORE_ENVIRONMENT=Development` per la build** (con una property function MSBuild
  che muta l'ambiente del nodo): risolve solo il punto 1, lascia in piedi 2 e 3, e sporca l'ambiente
  di tutta la build.
- **Scrivere il documento noi**, con un argomento tipo `--openapi-out` e un `Program` che esce dopo
  averlo scritto: funziona, ma è un secondo meccanismo accanto a quello che il design nomina, e
  soprattutto duplicherebbe la logica che il pacchetto Microsoft già ha.
- **Rendere `optional` la validazione OAuth in generale**: no. La garanzia «un'installazione senza
  client OAuth non parte» è di §2.2 e resta intera; l'unica eccezione è un processo che descrive
  l'API e non fa accedere nessuno.

## Correzione da portare nel design, se confermata

§7.4 e §9 punto 12: sostituire «senza avviare l'app» con «senza database e senza client OAuth: lo
strumento esegue l'entry point fino a `app.Run()`, perché è lì che gli endpoint minimal API
esistono, e `HubConfiguration.IsOpenApiDocumentGeneration` gli toglie da davanti la validazione di
Production, la validazione OAuth e `InitializeAsync`».
