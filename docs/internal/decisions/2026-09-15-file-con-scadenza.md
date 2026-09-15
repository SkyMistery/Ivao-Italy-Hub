# I file della media library con una scadenza

**Data:** 15 settembre 2026 — fase T0 di M2
**Stato:** il **che cosa** è deciso da Carmine nel design dei tour (`05-design-m2.md` §1.14, §15.2 n.21): banner e immagini dei tour li carica
il PRD nella media library, **un mese dopo la chiusura del tour un job li elimina** se non servono ad altro, e lo stesso servirà agli eventi.
**Chi collega le immagini al tour**: chi modifica il tour (`Tours.Edit`), dal selettore della media library (Carmine, 15 settembre). La
**forma** qui sotto è di Claude, da confermare nella revisione della PR di T0.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: un job del nucleo che **elimina file da solo**, e un'estensione di `IProjectable`.
**Estensione del nucleo** n.16 di `05-design-m2.md` §11. Fase **T4**.

## 1. Che cosa serve

- Una riga di modulo (il tour, poi l'evento) dice **quali file mostra e fino a quando le servono**.
- La media library non lascia eliminare un file che una riga di modulo usa ancora, come già fa per le pagine pubblicate.
- Un job elimina i file **i cui usi sono tutti scaduti**; basta un uso non scaduto, di chiunque, per tenerlo.

## 2. Che cosa c'è oggi, letto nel codice

- **L'indice degli usi** è `cms_content_references` (`ContentReferenceIndex`, G20): lo scrive la **pubblicazione di un contenuto**
  (`ContentPublishService`), per versione, con `ContentId` e `VersionId`. Una riga di modulo non ci entra.
- **Eliminare un file** (`MediaEndpoints.DeleteAsync`): rifiutato con `errors.media.inUse` se una pagina pubblicata lo usa; altrimenti la
  riga si segna eliminata (`DeletedAt`) e **resta**, perché una versione vecchia la nomina, e il file sul disco va via solo se nessuna
  versione lo mostra più (`HasFile = false`).
- ⚠️ **`IProjectable` oggi non funziona sulle righe di un modulo**: il contesto del modulo non ha le tabelle delle proiezioni e
  l'interceptor salta in silenzio (`ProjectionWriter.CanProject`). Scritto per esteso nella nota `2026-09-15-contatti-con-risposte` §3.3;
  la correzione è la prima cosa di T4, perché questa nota ne dipende.

## 3. La decisione

- **Una proiezione in più**: `ProjectionSnapshot` guadagna `MediaUses`, un elenco di `MediaUseProjection(mediaId, usedUntilUtc?)`
  (`null` = senza scadenza). Semantica **come la ricerca e il calendario**: riscritta per intero a ogni salvataggio della riga, per
  `source_module` + `source_id`, nella stessa transazione. Un tour **prorogato** sposta la scadenza da solo; un banner **cambiato** toglie
  l'uso vecchio.
- **Una tabella nuova, non la vecchia**: `cms_media_uses` (`source_module`, `source_id`, `media_id`, `used_until`), perché
  `cms_content_references` è per versione di un contenuto (`ContentId`, `VersionId`) e piegarla a una riga di modulo vorrebbe dire colonne
  nulle con un significato diverso a seconda di chi scrive. **Una sola domanda** resta però una: `ContentReferenceIndex.UsesOfMediaAsync`
  legge tutte e due, e la media library, l'editor («dove compare») e il job chiedono a lei.
- **Il tour dichiara** banner, foto del riquadro e **ogni file nominato nel briefing** (li trova il walker dei blocchi, che già estrae i
  file per G20) con scadenza `close_at + 1 mese`. Un file del briefing usato anche da una pagina pubblicata ha un uso senza scadenza e
  resta: non serve sapere «per chi è stato caricato». **Un tour nascosto** continua a dichiarare i suoi usi (nascosto non vuol dire
  finito); un tour in bozza anche, altrimenti il PRD potrebbe cancellare il banner mentre il FOD prepara la stagione.
- **Il job** `MediaExpiryJob`, giornaliero, nel nucleo (Quartz, `[DisallowConcurrentExecution]`, una riga in `hub_jobs_log`): prende i
  file che hanno **almeno un uso con scadenza**, **nessun uso senza scadenza o non ancora scaduto**, e nessun uso da una pagina
  pubblicata; li elimina con **la stessa logica** dell'eliminazione a mano (estratta da `MediaEndpoints.DeleteAsync` in un servizio, così
  «eliminare» vuol dire una cosa sola), poi toglie le righe d'uso scadute. Una riga di audit per file, con chi l'ha fatto: il job.
- **Un file che non ha mai avuto un uso con scadenza non si tocca mai**: il job non è una pulizia della libreria.
- **Il tour senza immagine** (dopo l'eliminazione, o in un fork senza banner) mostra un fondo neutro: nessun errore, nessun link rotto.
- **L'avviso nella libreria**: accanto a un file con usi a scadenza, «sarà eliminato il …» con la data più lontana, così il PRD lo sa.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il job del modulo che cancella i file dei tour chiusi | un modulo che scrive nella media library; e gli eventi lo riscriverebbero |
| Una colonna «scade il» sul file | un file usato da due tour e da una pagina ha tre scadenze, non una |
| Righe del modulo in `cms_content_references` | colonne per versione di un contenuto con un significato diverso per i moduli |
| Il tour che scrive l'uso con un servizio, dopo il salvataggio | la finestra fra i due salvataggi; il piano §16.4 dice «stessa transazione» proprio per questo |
| Nessuna eliminazione: si archiviano | Carmine vuole risparmiare spazio sul disco condiviso di Plesk |

## 5. Che cosa si tocca

- **Codice (T4)**: `ModuleDbContext` con le tabelle delle proiezioni escluse dalle migrazioni del modulo; `MediaUseProjection` in
  `Projections.cs` e nel writer; `cms_media_uses` e migrazione `AddMediaUses`; `UsesOfMediaAsync` sulle due tabelle; il servizio di
  eliminazione estratto; `MediaExpiryJob`; l'avviso nella lista dei media.
- **Test**: una riga di modulo proietta davvero (sul modulo di prova); un file con un uso scaduto e un uso vivo resta; con tutti gli usi
  scaduti va via, file e riga come nell'eliminazione a mano; un file senza usi a scadenza non si tocca; la proroga sposta la scadenza nella
  stessa transazione; il rollback del salvataggio non lascia l'uso.
- **Piano** 0.79: §9.1 (riga Media library), §16.4. **Design** `05-design-m2.md` §1.14 e §15.2 n.21.
