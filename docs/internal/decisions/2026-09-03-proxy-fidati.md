# I proxy di cui si crede l'header si dichiarano, e in produzione sono obbligatori

**Data:** 3 settembre 2026 — revisione senior di fine F4
**Stato:** decisa e implementata

## Il problema

F1 configurava i forwarded headers così:

```csharp
options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
options.KnownIPNetworks.Clear();
options.KnownProxies.Clear();
```

con il commento «Cloudflare e nginx stanno davanti, e i loro indirizzi non si conoscono in
anticipo». Svuotare tutt'e due le liste non significa «fidati di Cloudflare»: significa **fidati di
chiunque**. `X-Forwarded-For` è un header come un altro, e chiunque può metterlo.

Due cose poggiano sull'indirizzo che ne esce:

1. **Il rate limiter di `/auth/*`**, che partiziona su `context.Connection.RemoteIpAddress`. È
   l'unica cosa davanti al login, cioè al solo punto in cui un estraneo fa lavorare il server prima
   di aver dimostrato qualcosa. Con l'header creduto da chiunque, si aggira cambiando una stringa a
   ogni richiesta: i 10/minuto non esistono.
2. **La colonna `ip` di `hub_audit_log`** (piano §7), che questa stessa revisione ha smesso di
   lasciare vuota. Un audit il cui indirizzo lo sceglie chi scrive è peggio di un audit senza
   indirizzo, perché sembra un'informazione.

## La decisione

Le reti di cui si crede l'header si dichiarano in configurazione, in CIDR:

```json
{ "ForwardedHeaders": { "TrustedNetworks": ["173.245.48.0/20", "2400:cb00::/32"] } }
```

- **In produzione la lista è obbligatoria**: senza, l'applicazione non parte, con un messaggio che
  dice cosa manca e perché. È lo stesso trattamento di `AllowedHosts` (§2.3), per la stessa ragione:
  una configurazione che decide chi ti può mentire non ha un default sensato.
- **Fuori produzione**, se la lista è vuota il middleware **non entra affatto** nella pipeline:
  l'indirizzo è quello da cui la connessione è arrivata davvero. In sviluppo è quello giusto.
- Un valore che non è un CIDR è un errore di avvio con il valore citato per nome. Un refuso che
  disattivasse la fiducia in silenzio si scoprirebbe solo mesi dopo, come «il rate limiting non
  funziona».

## Alternative scartate

- **Tenere il comportamento e leggere `CF-Connecting-IP`.** Cablerebbe Cloudflare nel codice di un
  hub che deve poter essere forkato e messo dietro qualunque cosa, e comunque quell'header va
  creduto solo se arriva da Cloudflare: il problema si sposta, non si risolve.
- **Un default con le sole reti Cloudflare.** Un elenco che cambia, in un file che nessuno
  aggiornerebbe, per un'installazione che potrebbe non usare Cloudflare affatto.
- **Lasciare la lista facoltativa anche in produzione.** È la scelta che si stava già facendo, senza
  saperlo.

## Cosa deve fare chi installa

`docs/FORKING.md` e il `README.md` lo dicono adesso: le reti dei propri proxy, o `127.0.0.1/32` se
davanti c'è un reverse proxy locale. Vale la pena ricordare che i valori vanno in `secrets/*.json`
o nelle variabili d'ambiente, non in `division.json`: non sono comportamento della divisione, sono
la forma dell'installazione.
