# Il motto e chi ha costruito il sito, nel footer

**Data:** 17 ottobre 2026 — PR del nucleo, ramo `feat/footer-tagline-credits`, versione 0.9.0
**Stato:** **chiesta da Carmine** (17 ottobre 2026, in chat al master, con il footer di vIPI davanti: le due cose sottolineate, il motto
«it takes time» sotto il marchio e «Built by» con Andrea (562466) e Carmine (704798), ciascuno un link al suo profilo IVAO). Le scelte di
forma qui sotto sono **tecniche**, prese dalla sessione di lavoro; una sola parola resta da confermare a Carmine (§5).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: nessun meccanismo nuovo. La configurazione della divisione (`division.json`,
`DivisionOptions` e il suo validatore), l'unico bootstrap (`/api/me`) e il perimetro di IVAO (`Core/Ivao/`) esistono già e coprono il
caso; si aggiungono due campi al primo, due al secondo e una funzione al terzo. È una PR del nucleo perché il footer, il bootstrap e la
configurazione sono del nucleo (`CLAUDE.md` §0 regola 6).

## 1. Che cosa serve

Il sito ATC di IVAO Italia (vIPI) scrive nel footer, in corsivo sotto il nome, il motto della divisione, e in basso a destra, sotto «Part
of the International Virtual Aviation Organisation», chi l'ha costruito, con un link al profilo di ciascuno. Carmine li vuole uguali
nell'hub.

## 2. Dove stanno i dati: `division.json`, non `locales/`

Le parole del footer che sono di ogni divisione («Part of…», il disclaimer, i link legali di IVAO) stanno in `locales/`, e il commento di
`division.example.json` dice che «content, links and translations are not configuration». Il motto e i nomi però **non sono di ogni
divisione**: sono di questa installazione, come il nome della divisione e il suo marchio (`name`, `logoUrl`), che già stanno in
`division.json`. Messi in `locales/`, un fork che copia i file delle lingue (e li copia tutti: sono del nucleo) si troverebbe il motto di
IVAO Italia e due VID italiani nel suo footer, e la prova della divisione finta «XX» avrebbe ragione a rifiutarli. In `division.json`:

- `tagline`: **un valore per lingua**, `{ "it": …, "en": … }`, come `name`. Facoltativo; ma se c'è, c'è in **ogni** lingua di `locales`
  (il validatore rifiuta la partenza altrimenti), con la stessa regola del nome: un lettore non trova mai il footer in una lingua che non
  ha scelto. Traducibile e non una stringa sola perché un motto in genere si traduce; per «it takes time» Carmine scrive la stessa frase
  due volte, e non costa niente. Una stringa sola avrebbe obbligato una divisione che lo traduce a cambiare il formato dopo.
- `builtBy`: un elenco di `{ "name": …, "vid": … }`, nell'ordine in cui il footer li nomina. Facoltativo; il validatore rifiuta un nome
  vuoto, un VID che non è un numero di socio (≤ 0) e lo stesso VID due volte.

Assenti — come nell'esempio e nella divisione «XX» — il footer non disegna né il motto né la riga.

## 3. L'indirizzo del profilo: nel perimetro di IVAO

`https://www.ivao.aero/Member.aspx?Id=<VID>` nomina IVAO, quindi non sta in uno schermo né in `division.json` (dove andrebbe ripetuto per
ogni persona). Lo scrive **una sola funzione**, `IvaoMemberProfile.Url(vid)` in `Core/Ivao/`; il bootstrap porta per ogni costruttore
`{ name, vid, profileUrl }` e il footer usa `profileUrl` così come arriva. Oggi nessun'altra pagina dell'hub collega un profilo IVAO
(cercato: nessun `Member.aspx` nel codice); il giorno che servirà, la funzione c'è.

## 4. Il footer

- Il motto: `<p>` in corsivo sotto la riga marchio + nome, nella colonna della divisione.
- «Built by»: nella riga in fondo, sotto «Part of…», allineato a destra da `md` in su e centrato sul telefono, come il resto della riga.
  È l'unica eccezione alla regola «niente link nella riga in fondo» del 10 settembre: un nome che non porta alla persona non si può
  verificare. Ogni nome è «Nome (VID)», link in una nuova scheda con `rel="noreferrer noopener"`, come i link legali.
- Le parole sono chiavi i18n: `footer.builtBy`, e i separatori `footer.listSeparator` («, ») e `footer.listLastSeparator` (« & »), così
  con tre persone si legge «A, B & C» e una lingua che preferisce «e» lo scrive nel suo file.
- Nessun componente nuovo fuori dall'elenco chiuso: un `<p>` e degli `<a>` dentro `AppFooter`, con le classi del footer.

## 5. Una parola per Carmine

`footer.builtBy` in italiano è «Realizzato da». vIPI mostra «Built by» anche nella pagina italiana? Se Carmine lo vuole in inglese in
entrambe le lingue, si cambia una riga di `locales/it/common.json`.

## 6. Che cosa non fa

- I VID dei costruttori in `division.json` non passano dalla cancellazione dei dati di una persona (`IPersonalDataEraser`): sono
  configurazione scritta dal maintainer, non righe del database, e chi è nominato lo è per sua scelta. Se uno chiede di toglierlo, si
  toglie dal file.
- Nessuna riga «Tested by» (vIPI ce l'ha, Carmine non l'ha chiesta): sarebbe un secondo elenco della stessa forma, da aggiungere solo su
  richiesta.

## Da portare nel piano

- §4.1 (che cosa sta in `division.json`): `tagline` (per lingua, facoltativo, tutto o niente) e `builtBy` (nome + VID, facoltativo).
- §4.2 (il perimetro di IVAO): `Core/Ivao/IvaoMemberProfile` è l'unico posto che scrive l'indirizzo del profilo di un socio.
- §16 (il bootstrap `/api/me`): `division.tagline` e `division.builtBy[] { name, vid, profileUrl }`.
- Il footer (design M1, la parte del frame): il motto sotto il nome; «Built by» sotto «Part of…», unica riga con link in fondo.
