# Writing an interactive block for this hub

Hand this file to whoever writes the animation — a person or an assistant such as Claude Code — and
paste what comes back into the **Code** field of an interactive block. Everything below is the
contract: what your fragment is given, what it may not do, and what it will be judged on.

## What you are writing

**One HTML fragment**: markup, an optional `<style>`, an optional `<script>`. No document, no
`<html>`, no `<head>` — the hub wraps your fragment in the shell quoted at the end of this file.

- **JavaScript of the browser, ES2022.** No TypeScript, no `import`, no npm, no CDN, no `fetch`.
  This is not a matter of taste: the frame runs under `default-src 'none'`, so a request to anywhere
  is refused by the browser. Everything your fragment needs must be inside your fragment.
- **Prefer SVG and CSS.** An animation that plays by itself needs no JavaScript at all: draw it in
  SVG and move it with `@keyframes`. That version scales, reads on a screen reader, and honours
  "reduce motion" without you writing a line for it.
- **JavaScript when there are choices**, which is the other thing this block is for: buttons that
  change what is drawn. Keep the state in one variable and redraw from it.
- **Canvas only when many things move at once** — twenty aircraft on a radar scope. For a runway and
  a circuit, SVG.

## What the frame gives you

```js
HUB.locale; // 'it' | 'en' | … — the language the page is being read in
HUB.dark; // true when the block sits on a dark ground or the reader is in the dark theme
HUB.reducedMotion; // true when the reader asked their system for less movement
HUB.t({ en: 'Left circuit', it: 'Circuito sinistro' }); // the string in HUB.locale
window.addEventListener('hub:theme', (event) => event.detail.dark); // the ground changed under you
```

The colours are CSS variables, and they are the division's own — use them and nothing else:
`--ink`, `--ink-quiet`, `--line`, `--brand`, `--ocean`, `--aurora`, `--artifice`, `--ok`, `--warn`,
`--stop`. The background is **transparent on purpose**: the section behind the frame is what a reader
sees around your drawing, so never paint a ground of your own.

The height is measured for you: the frame tells the page how tall your fragment is and the page
gives it that room. Do not set a height on `body`.

## What is required, not suggested

1. **Both languages.** Every word you draw goes through `HUB.t({ en: …, it: … })`. A reader of the
   other language must not find your buttons in English.
2. **A keyboard.** If there are choices, they are `<button>` elements in the document, reachable by
   tab and operable by Enter and Space. A choice that only a mouse can make does not exist for part
   of your readers. Mark the chosen one with `aria-pressed`.
3. **Movement is optional for the reader.** CSS animations are already stopped for somebody who
   asked for less movement; anything you animate from JavaScript must check `HUB.reducedMotion` and
   draw the final state instead. An animation that runs for more than a few seconds needs a way to
   stop it.
4. **It must read at 360 pixels wide.** Use `viewBox` and let the SVG scale; do not set pixel widths.
5. **Say what it is.** The block itself carries a name and a description — fill them in. They are the
   only part of an interactive block that the search index sees, and the only part that survives on
   paper: **printing folds the frame away**, so a document whose meaning depends on the animation
   must say that meaning in the description or in the text above it.

## How it should look

These are not suggestions about taste. Twenty of these will be written over the years by different
people, and they have to read as one family — the way every other part of this hub does, because it
is all drawn from one design system. A frame cannot use that design system (it has no stylesheet of
ours and no network), so the rules it would have given you are written out here.

### Colour means something

Nine variables, and each has a job. Using them for anything else is how two animations end up
disagreeing about what orange means.

| Variable | What it is for |
| --- | --- |
| `--ink` | Anything a reader reads: labels, numbers, the outline of a fixed thing |
| `--ink-quiet` | Structure that is context rather than subject — a runway, a coastline, a grid |
| `--line` | Hairlines, ticks, the edge of a box |
| `--ocean` | The **route**: a path, a circuit, a track, the thing being explained |
| `--artifice` | The **moving** thing, and only it: an aircraft, a vehicle, the token that travels |
| `--aurora` | A second route or a second party, when one drawing holds two |
| `--brand` | Rarely, and never as decoration: something that **is** the division — a mark, the title of the figure. A drawing of a procedure is not a place for a brand colour |
| `--ok` `--warn` `--stop` | Only where the colour **is** the meaning: cleared, caution, refused. Never decoration |

⚠️ **At most four colours in one drawing**, counting the ink. More than that and nobody reads the
legend; they guess, and on a procedure a guess is the fault this block exists to avoid. And never
colour alone: whatever a colour tells a reader, a label or a shape has to tell them too — a
controller who cannot tell your green from your orange still has to be able to use the picture.

### Lines, shapes and size

- Draw in an **SVG with a `viewBox`** and no width or height in pixels. 400 × 240 is a good default:
  it fills a reading column and still reads on a phone.
- **Stroke 2** for the subject, **1.5** for structure, **1** for hairlines — in `viewBox` units, on a
  400-wide canvas. Below 1 a line disappears when the frame is scaled down.
- **Dashes mean "not a thing, a path"**: `stroke-dasharray="6 4"` for a route or an intention, solid
  for something that physically exists.
- **Text inside the drawing is 11 to 14 units** on that same canvas, never smaller, and it is
  **the reader's system font** — the frame cannot load ours. So: few words, short words, no
  sentences. Prose belongs to the document above the block, which is written in the site's own type
  and is the part that reaches search and paper.
- **Rounded joins** (`stroke-linejoin="round"`), because an aviation drawing is full of corners and
  square joins read as noise at small sizes.

### Layout

- **One figure per block.** Two ideas are two blocks; the page is what puts them in order.
- **Controls under the drawing**, in a row, wrapping on a narrow screen. A control is a `<button>`,
  at least 44 px tall including its padding, with `aria-pressed` when it is a choice among several.
- **Nothing scrolls inside a frame.** If it does not fit, the drawing is too big: make the `viewBox`
  wider, not the frame taller.
- **A legend only when a symbol is not obvious**, and then as text beside the drawing rather than
  floating on it.

### Movement

- **Slow.** A circuit takes 8 to 15 seconds to fly; anything faster reads as a flicker and teaches
  nothing. Loop it, and give a **stop** control when it loops.
- **One thing moves at a time.** Two aircraft moving together are a diagram of chaos unless the
  point *is* the two of them.
- `HUB.reducedMotion` is not a preference to honour when convenient: when it is true, draw the end
  state — the aeroplane on the downwind leg, the traffic where the text is talking about — and stop.
- No easing tricks, no bounce, no fade-in of the whole picture. What moves is what the document is
  explaining.

### What a picture of an airfield says

These conventions come from the documents this block lives in, so that two SOPs never draw the same
thing two ways:

- **The runway is horizontal** in a circuit drawing, with its designators at both ends (`09` on the
  left, `27` on the right). Do not rotate it to true north: the reader is following a procedure, not
  navigating.
- **North up** in anything that is not a circuit — an airspace, a sector, a taxi route — with a small
  north mark.
- **Altitudes carry their unit** (`1500 ft`), headings are three digits (`090`), frequencies carry
  three decimals (`118.700`). The same as the document says them, word for word.
- **A position is its callsign** as the document writes it (`LIRF_TWR`), never "the tower".

### Never

Gradients, shadows, glows, 3D, drop caps, photographs, textures, more than four colours, text under
11 units, a hairline under 1, an animation that is the only place some information appears.

### Before you hand it over

- [ ] Every word goes through `HUB.t` and exists in both languages.
- [ ] Every choice is a `<button>`, reachable by tab, marked with `aria-pressed`.
- [ ] `HUB.reducedMotion` draws a still picture that still makes the point.
- [ ] It reads at 360 px wide and in the dark (try `HUB.dark`).
- [ ] Four colours at most, each doing the job the table above gives it.
- [ ] Nothing in it fetches anything.
- [ ] Under 64 KB.
- [ ] The drawing agrees with the text above it.

## What you must not do

- No network of any kind — no fonts, no images from elsewhere, no analytics, no `fetch`.
- No `localStorage`, no cookies: the frame has an opaque origin and storage throws there.
- No attempt to reach the page around you. `parent`, `top` and `document.referrer` give you nothing;
  the sandbox sees to it, and code that tries is code that will be refused at review.
- Nothing larger than **64 KB**. If your fragment is bigger than that, it is doing too much: an
  animation that explains a procedure is 5 to 15 KB.

⚠️ **And none of the three fails quietly.** The size is refused when the page is saved, with the
message on the field and a count of bytes beside the box while you type. The other two are refused by
the browser — there is no network here and no storage, whatever the code asks for — and the frame
**reports the refusal to the page**, which draws it under the animation **for the staff only**: "the
browser refused this animation something it asked for (connect-src)". An animation that throws says
that too. So a fragment that ignores this section does not produce a mystery; it produces a line
naming what it tried, in the editor, where whoever pasted it is standing. A reader is told nothing:
it is not their animation to fix.

## And the part no mechanism can check

An animation that contradicts the text above it is worse than no animation: a circuit drawn to the
right under a procedure that says left will be believed by somebody. Whoever publishes it answers for
it — check it against the document, and use the review date and the publication window ("what
changed") the way you would for any other part of an operational document.

## A worked example: runway 09/27, left hand circuit

This is the whole of a block: it draws a runway, flies a circuit around it, lets the reader choose
which side the circuit is on, and stops when asked.

```html
<figure style="display: flex; flex-direction: column; gap: 0.75rem">
  <div role="group" id="choices" style="display: flex; gap: 0.5rem; flex-wrap: wrap"></div>

  <svg viewBox="0 0 400 240" width="100%" aria-labelledby="circuit-title">
    <title id="circuit-title"></title>

    <!-- the runway, 09 at the left end and 27 at the right -->
    <g stroke="var(--ink)" fill="var(--ink)">
      <rect x="120" y="150" width="160" height="14" fill="var(--ink-quiet)" stroke="none" />
      <text x="112" y="161" font-size="11" text-anchor="end">09</text>
      <text x="288" y="161" font-size="11">27</text>
    </g>

    <!-- the circuit, drawn as one path so the aeroplane can follow it -->
    <path
      id="circuit"
      d="M 140 157 L 300 157 L 330 120 L 330 70 L 300 40 L 140 40 L 110 70 L 110 120 Z"
      fill="none"
      stroke="var(--ocean)"
      stroke-width="2"
      stroke-dasharray="6 4"
    />

    <g id="aeroplane" fill="var(--artifice)">
      <polygon points="0,-6 10,0 0,6 2,0" />
    </g>
  </svg>

  <p id="legend" style="margin: 0; color: var(--ink-quiet); font-size: 0.85rem"></p>
</figure>

<script>
  (function () {
    var svg = document.querySelector('svg');
    var path = document.getElementById('circuit');
    var plane = document.getElementById('aeroplane');
    var legend = document.getElementById('legend');
    var title = document.getElementById('circuit-title');
    var choices = document.getElementById('choices');

    var words = {
      left: { en: 'Left hand circuit', it: 'Circuito sinistro' },
      right: { en: 'Right hand circuit', it: 'Circuito destro' },
      stop: { en: 'Stop', it: 'Ferma' },
      play: { en: 'Play', it: 'Avvia' },
      legend: {
        en: 'Runway 09/27. The traffic joins downwind and turns base at the end of the runway.',
        it: 'Pista 09/27. Il traffico entra in sottovento e vira in base alla fine della pista.',
      },
    };

    var hand = 'left';
    var running = !HUB.reducedMotion;
    var distance = 0;

    /* The right hand circuit is the same path mirrored about the runway, which is what a mirrored
       transform on the group says — no second path to keep in step with the first. */
    function draw() {
      svg.setAttribute('data-hand', hand);
      path.setAttribute('transform', hand === 'right' ? 'matrix(1 0 0 -1 0 314)' : '');
      title.textContent = HUB.t(words[hand]);
      legend.textContent = HUB.t(words.legend);
    }

    function button(key, label, pressed, onClick) {
      var element = document.createElement('button');
      element.type = 'button';
      element.textContent = label;
      element.setAttribute('aria-pressed', String(pressed));
      element.addEventListener('click', onClick);
      choices.appendChild(element);
      return element;
    }

    function controls() {
      choices.textContent = '';
      button('left', HUB.t(words.left), hand === 'left', function () {
        hand = 'left';
        draw();
        controls();
      });
      button('right', HUB.t(words.right), hand === 'right', function () {
        hand = 'right';
        draw();
        controls();
      });
      button('run', HUB.t(running ? words.stop : words.play), running, function () {
        running = !running;
        controls();
        if (running) {
          requestAnimationFrame(step);
        }
      });
    }

    var last = 0;
    function step(now) {
      if (!running) {
        return;
      }

      var length = path.getTotalLength();
      distance = (distance + (now - last) * 0.06) % length;
      last = now;

      var point = path.getPointAtLength(distance);
      var ahead = path.getPointAtLength((distance + 6) % length);
      var angle = (Math.atan2(ahead.y - point.y, ahead.x - point.x) * 180) / Math.PI;
      var mirror = hand === 'right' ? 'matrix(1 0 0 -1 0 314) ' : '';
      plane.setAttribute('transform', mirror + 'translate(' + point.x + ' ' + point.y + ') rotate(' + angle + ')');

      requestAnimationFrame(step);
    }

    draw();
    controls();

    /* Reduce motion: the aeroplane is placed on the downwind leg and left there. */
    if (HUB.reducedMotion) {
      var point = path.getPointAtLength(path.getTotalLength() * 0.6);
      plane.setAttribute('transform', 'translate(' + point.x + ' ' + point.y + ') rotate(180)');
    } else {
      requestAnimationFrame(function (now) {
        last = now;
        step(now);
      });
    }

    window.addEventListener('hub:theme', draw);
  })();
</script>
```

## The shell your fragment is wrapped in

Everything above is a promise this file makes; this is the file that keeps it. It is served from the
same place these guidelines are, so the two cannot disagree.

```html
{{shell}}
```
