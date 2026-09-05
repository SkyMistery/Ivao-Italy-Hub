# I template di sistema li vede solo il dipartimento Web

**Data:** 5 settembre 2026 — trovata in G0 di M1, **aperta**
**Serve una decisione di Carmine.** G0 si è chiusa senza toccarla: è fuori dal suo perimetro.

## Cosa succede

`ContentTemplateSeeder` semina i tre template di sistema (`section-page`, `about`, `policy`) con
`OwnerDepartment = WD`. `Content.View` è un permesso **di dipartimento**, e il global query filter
applica la stessa regola alla lista dei template.

Conseguenza, verificata nel browser durante G0 con un coordinatore ED vero (posizione `IT-EC`):

- `GET /api/content?filter[isTemplate]=true` risponde **zero righe**;
- `TemplatePicker` non rende niente quando i template sono zero (è scritto così apposta: un select
  vuoto invita a cliccare);
- quindi per un coordinatore che non sia del dipartimento Web **«Nuovo da template» non esiste**, e
  l'unica via è la pagina vuota.

Un coordinatore Web (`IT-WM`) li vede tutti, e infatti il giro di G0 gira come lui.

## Perché conta adesso

Non è un difetto di G0 e non blocca M1, ma tocca tre fasi che arrivano:

- **G5** (news e documenti): ogni dipartimento crea le proprie righe, e il modo previsto per farlo
  è partire da un template;
- **G8** (pagine di sistema seedate): stessa famiglia di righe;
- **G11** (differenze rispetto al template): l'editor legge il template di una pagina per `key` di
  sezione. Se il template appartiene a un altro dipartimento, un coordinatore che apre una pagina
  nata da quel template **non può leggerlo** — le differenze non gliele può mostrare nessuno.
  Questa è la conseguenza meno ovvia e la più fastidiosa.

## Le tre strade, con una raccomandazione

1. **I template di sistema sono leggibili da tutti gli staff, e modificabili solo da chi ha
   `Content.ManageTemplates` sul dipartimento che li possiede.** È una riga nella policy di lettura
   di `MapCrud` per `Content` quando `is_template = true`, non un handler nuovo. Coerente con come
   il prodotto li descrive: «pagine di sistema», non «pagine del dipartimento Web».
   **Raccomandata**: è la lettura che il resto del design dà già per scontata (M1 §8.2, §9.1), ed è
   l'unica che fa funzionare G11 per tutti.
2. **Ogni dipartimento riceve una copia dei template al seed.** Nessun cambio di policy, ma
   moltiplica le righe per dipartimento, e una correzione a un template di sistema in una release
   successiva non raggiunge le copie. Contro `CLAUDE.md` §2.
3. **Si lascia com'è**: i template li usa solo il dipartimento Web, che costruisce il sito. È
   difendibile per le pagine, non lo è per news e documenti di G5.

## Nel frattempo

Il banco e2e firma come coordinatore **Web** (`IT-WM`), il che è realistico — è chi costruisce il
sito — ma va saputo: quel ruolo raggiunge ogni dipartimento, quindi il giro di G0 **non** esercita
la guardia di dipartimento. Quella resta coperta da `web/e2e/back-office.spec.ts`, che gira con un
coordinatore di un dipartimento solo.
