# `IProjectable.Project()` riceve un `ProjectionContext`

**Data:** 3 settembre 2026 — fase F4
**Stato:** **confermata da Carmine il 3 settembre 2026**. `01-design-m0.md` §3.6 corretta, changelog del
piano 0.23.

## Il design

`01-design-m0.md` §3.6 scrive la firma così:

```csharp
public interface IProjectable
{
    string SourceModule { get; }
    string SourceId { get; }
    ProjectionSnapshot? Project();
}
```

e §5.1 dice che `Content` «proietta `Search` con testo estratto dal walker §5.3».

## Il problema

Le due cose non stanno insieme. `Project()` non ha parametri, ma per proiettarsi un contenuto ha
bisogno di:

1. **le lingue della divisione**, perché `SearchProjection.Title` e `Text` sono `Localized<string>`
   e la riga di ricerca esiste **una per lingua**;
2. **il `BlockDocumentWalker`**, che estrae il testo dei blocchi e che, per sapere se un oggetto
   dentro `props` è un valore tradotto, deve conoscere quelle stesse lingue (design §5.3).

Un'entità EF non si fa iniettare niente: non è un servizio, la costruisce il change tracker. Le
alternative erano tutte peggiori:

- cablare `["it", "en"]` nell'entità → è esattamente ciò che un hub forkabile non può fare;
- indovinare quali chiavi di un oggetto JSON sono lingue con un'euristica → il design dà la regola
  esatta («tutte le chiavi sono lingue della divisione») e un'euristica la tradirebbe;
- far estrarre il testo al `ProjectionWriter` con un ramo speciale per `Content` → il writer
  smetterebbe di essere generico, che è tutto il suo valore.

## La decisione

Un solo parametro, immutabile, costruito una volta dal contenitore:

```csharp
ProjectionSnapshot? Project(ProjectionContext context);

public sealed record ProjectionContext(
    IReadOnlyList<string> Locales,
    string DefaultLocale,
    BlockDocumentWalker Blocks);
```

Lo passa l'interceptor, che è l'unico chiamante. Un modulo che proietta le proprie righe riceve le
lingue della divisione senza doverle cercare, che è la stessa ragione per cui esiste il tipo.

Il resto di §3.6 non cambia: `null` continua a voler dire «togli ogni proiezione», la convenzione
«una bozza non si proietta» resta nell'interceptor e non nelle entità, e l'upsert resta per chiave
`(source_module, source_id)`.

## Esito

Sì. La firma di `01-design-m0.md` §3.6 è stata corretta e il `ProjectionContext` è documentato lì
accanto. L'alternativa scartata era spostare l'estrazione del testo fuori dall'entità, con
`SearchProjection` che avrebbe dovuto portare il corpo grezzo invece del testo già estratto.
