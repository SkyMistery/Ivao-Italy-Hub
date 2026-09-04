# «Nuovo da template» è una rotta sua, non una query su `POST /api/content`

**Data:** 4 settembre 2026 — F7
**Stato:** **decisa** (Carmine, 4 set 2026)

## Cosa serviva

Il design M0 §5.6 e il piano di implementazione §D (F7, task 4) scrivono la creazione da template
così: `POST /api/content?templateId=` → copia profonda.

## Perché non si può scrivere esattamente così

`POST /api/content` **è già mappata**: la genera `MapCrud`, ed è la creazione normale, con il suo
`ContentWriteDto` nel corpo. ASP.NET Core instrada per percorso e metodo, mai per query string:
due handler sullo stesso `POST /api/content` non si distinguono, e non esiste un «prosegui al
prossimo» nelle minimal API.

Le uscite possibili erano tre:

1. far leggere `?templateId=` all'handler generato di `MapCrud` — cioè insegnare al motore CRUD che
   esiste una cosa chiamata template. È esattamente il caso speciale che §16.6 vieta;
2. un corpo di richiesta che a volte è un `ContentWriteDto` e a volte no. Il documento OpenAPI
   descriverebbe una `POST` con un corpo che «dipende», e il client generato smetterebbe di essere
   tipizzato proprio sul punto in cui serve;
3. una rotta sua.

## Cosa si è fatto

`POST /api/content/from-template/{templateId}` con corpo `{ ownerDepartment, slug }`, registrata
**sullo stesso `RouteGroupBuilder`** che `MapCrud` restituisce, accanto a `POST /{id}/publish` e a
`GET /public/{kind}/{slug}`. Il motore non sa niente dei template; le tre cose che una pagina sa
fare e un link no stanno tutte fuori dal motore, nello stesso file.

Il corpo porta `ownerDepartment` e `slug` perché sono le due cose che il template non può sapere: a
quale dipartimento appartiene la pagina nuova, e a che indirizzo sta. Tutto il resto — `kind`,
titolo, sommario, corpo — è copiato dal template.

La copia profonda resta lato server: identificativi nuovi per ogni sezione e ogni blocco, `frozen`
azzerato, e `required`/`locked`/`allowedBlocks` **tolti**, perché una pagina che li portasse
potrebbe sollevarsi da sola le restrizioni (ed è ciò che il validatore dell'envelope rifiuta,
design §5.2).

## Cosa si tocca

- `docs/internal/01-design-m0.md` §5.6: la riga dell'endpoint.
- Niente altro: permessi, copia, validazione e test restano come il design li descrive.

## Confermata

Carmine, 4 settembre 2026. Le due alternative gli sono state messe davanti — un nome diverso per la
rotta, oppure la forma con la query string pagando la tipizzazione del client su quella chiamata —
e la rotta resta questa.
