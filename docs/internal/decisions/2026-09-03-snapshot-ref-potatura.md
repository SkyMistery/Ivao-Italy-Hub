# Lo snapshot `ref_` è uno snapshot: quello che IVAO non elenca più se ne va

**Data:** 3 settembre 2026 — revisione senior di fine F4
**Stato:** decisa e implementata

## Il problema

`RefDataSyncJob` faceva solo upsert. Un centro o un aeroporto che IVAO smetteva di elencare restava
in tabella per sempre.

Non è un problema estetico. `IFirDirectory` legge gli id di `ref_ivao_centers` ed è ciò che
distingue `LIRR-CH` («capo di una FIR di questa divisione») da una stringa qualunque. Una FIR
dismessa continuava quindi a **produrre posizioni staff riconosciute**, cioè permessi, a tempo
indeterminato.

## La decisione

Il job cancella le righe che una risposta **non vuota** non contiene più.

La condizione «non vuota» è la rete di sicurezza, ed esisteva già: i due metodi tornano subito
quando IVAO risponde con zero record, con il commento «tenersi lo spazio aereo di ieri batte
svuotare la tabella». La potatura sta **dopo** quel controllo, quindi un pomeriggio storto di IVAO
non può essere letto come «la divisione non ha più spazio aereo».

Che sia corretto dipende da un fatto misurato, non supposto: `/v2/centers?countryId=IT` e
`/v2/airports/all?countryId=IT` rispondono con **l'insieme completo** per quel paese (7 centri e 221
aeroporti per l'Italia, HANDOFF §2). Non sono endpoint paginati né incrementali. Se un giorno lo
diventassero, questa decisione va riaperta: sarebbe l'unico modo di far sparire dati veri.

Le righe cancellate finiscono nel log con i loro id, perché una cancellazione automatica che non
lascia traccia è una cancellazione che nessuno saprà spiegare.

## Perché è sicuro cancellare

Il piano §16.12 vieta le foreign key fra contesti: le tabelle `ref_` sono referenziate al massimo da
colonne `icao`/`centerId` non vincolate. Nessuna riga di nessun modulo si rompe se una sparisce.

## Alternative scartate

- **Una colonna `retired_at` invece della cancellazione.** Conserva la storia, ma ogni lettore
  dovrebbe ricordarsi di filtrarla — e il primo che se ne dimenticherebbe è `IFirDirectory`, cioè
  esattamente il caso da cui siamo partiti.
- **Lasciare tutto e filtrare per `synced_at`.** Stessa obiezione, con in più una soglia da
  scegliere a caso.
