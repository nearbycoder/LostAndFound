# Lost & Found: Design and Technical Plan

> **Pitch:** You run the lost-property desk at Ninefold Junction, a station where lost things come in from other times. Inspect each object, catch the liars, and decide which belongings must never be returned.

Working title: **Lost & Found**. Setting: *Ninefold Junction Lost Property Office*, Monday 15 to Friday 19 October 1962.

---

## 1. Design pillars

1. **The desk is tactile.** Every verb is physical. Drawers slide with weight and rattle, tags swing up when you look at them, objects turn in your hands, latches click, rubber stamps thump, and the ticket printer chatters out a receipt. You should want to open drawers just to hear them.
2. **Small, fair deductions.** You can solve every case from what is on the desk: the intake tag, the object's hidden details, the claimant's story, and Agnes's rules. The good moment is finding the hidden detail that breaks a story, or proves it.
3. **The people are distinctive.** Each commuter has a clear silhouette, colour, voice timbre, verbal habit and walk. By Friday you know them on sight. Recurring characters get callbacks.
4. **The uncanny is gentle.** It aims for wonder and melancholy, not horror. A suitcase hums for its owner, a photograph is dated tomorrow, a soldier's tin is cold to the touch, and when you return a ring every photograph on the desk changes.

**Out of scope:** combat, a walkable world, timers that punish thinking, inventory management, branching dialogue trees.

---

## 2. Core loop

### Moment to moment (one case, 60 to 120 s)
1. **Ring the bell.** The next commuter walks up to the window with footsteps and an idle animation.
2. **Listen.** They describe what they lost in short lines with voice babble. **Key claims** are highlighted and written onto the **claim slip** in handwriting as they speak.
3. **Search.** Turn to the drawer cabinet (left) or the shelf (right). Open drawers and hover items to read their **intake tags** (where, when, which train, porter's notes).
4. **Inspect.** Pick up a candidate. It flies to your hands. Drag to rotate, scroll to zoom. A magnifier cursor glints near hidden **details**. Click them to discover: open the latch, flip the photo, read the engraving, wind the key, or shine Agnes's blue lamp (from Day 4). Each discovery becomes a **clue** on the slip.
5. **Ask.** Click a clue to ask the claimant about it with a neutral question ("Anything written inside?"). Honest owners answer correctly. Liars only know what they could have seen, so they bluff.
6. **Decide** by stamping the slip:
   - **RETURN** (green): the item is on the counter tray and goes to the claimant. In two-claimant cases you stamp that claimant's half of the slip.
   - **REFUSE** (red): nobody gets it and the item stays in storage.
   - **SEAL** (iron): the item goes into the **Iron Drawer** for good, for things that must never be returned.
7. **Receipt.** The printer prints the receipt and the claimant reacts. You don't see a verdict yet. The receipt goes on the spike, which fills up over the day.

### Per day (5 to 8 min)
Morning: flip the desk calendar. Agnes's note introduces **one new rule**. Gus the porter drops off the night's intake. Then 4 to 5 cases. Evening: the **Day Ledger** stamps each case correct or wrong with the reason, and the **Ninefold Gazette** headline shows the consequences. Photographs or the desk may change, and a cliffhanger leads into tomorrow.

### Per week
Five days, one story. Choices carry forward: items you refuse stay for their real owner, sealed items stay sealed, and anything you give the Grey Gentleman makes him stronger and the station greyer. There are three endings and an epilogue that reacts to each major choice.

### "One more run"
- **Day Select** lets you replay any finished day for 3 stamps. It keeps the story state from your latest run.
- **Curio Ledger**: every one of the 25 objects has one optional **secret detail** (a love note in a lining, a song in a seashell). Find all 25.
- Three endings, and the epilogue's variant lines invite another week.

---

## 3. Rules of the desk (what the player learns)

Agnes Pell ran the desk for 41 years and left her rules as handwritten notes in the drawers, one per morning, plus a few that turn up mid-shift. They are collected on a card pinned to the desk that you can read on hover at any time.

| # | Rule (Agnes's wording) | Unlocked | Mechanical meaning |
|---|---|---|---|
| 1 | "Every stray has a tag. A real owner knows where they lost it." | Day 1, morning | A story that contradicts the tag (place, day, train) means **REFUSE**. |
| 2 | "Trust the object, not the story. Liars only know what they can see." | Day 1, case 2 | A wrong answer about a hidden detail means **REFUSE**. Visible features prove nothing. |
| 3 | "If it hums, it's home, whatever they say." | Day 1, case 4 | An object that hums for a claimant belongs to them, even if their story is muddled. **RETURN**. This overrides rules 1 and 2. |
| 4 | "Two claim one? Let the object decide." | Day 2, morning | In two-claimant cases, the hidden details point to exactly one person. |
| 5 | "The Grey Gentleman gets nothing. Not even the time of day." | Day 2, morning | Never RETURN anything to Mr Vell. |
| 6 | "If it comes from tomorrow, it isn't lost yet. Lock it away." | Day 2, mid-shift (in the Iron Drawer) | A future date on tag or object means **SEAL**, even if the claimant is the real owner. |
| 7 | "Frost means they've gone on ahead. Cold things go only to the cold." | Day 3, morning | Frosted objects go to a "cold" claimant (window frosts, voice echoes): **RETURN**. A living claimant: **REFUSE**. |
| 8 | "My blue lamp shows what ink tries to hide." | Day 4, morning | UV lamp tool. It shows hidden ink, including the blue date stamp on every Ninefold ticket. |

The desk calendar always shows today's date, so the player can judge what counts as "tomorrow".

---

## 4. Mechanics in detail

### 4.1 Views and camera
You are seated in the booth. There are three yaw positions and a smooth eased turn between them (A/D, arrow keys, or moving the mouse to the screen edge, with clickable edge arrows):
- **Counter** (default): the commuter behind the glass window, with the desk below (slip, stamps, printer, bell, lamp, photographs, calendar, tray).
- **Cabinet** (left): six wooden drawers A to F with brass label holders and handwritten cards.
- **Shelf** (right): three shelves for large items, with the **Iron Drawer** (riveted, keyhole, chain) below.

**Inspect mode**: the item floats in front of the lamp with background depth of field, and the camera leans in slightly. Subtle breathing sway everywhere. A short ease-out on every turn.

### 4.2 Objects
Every object has:
- an **intake tag**, readable without inspecting: where found, when found (day and time), train, and porter notes;
- 2 to 4 **case details** (hotspots) that matter to deduction;
- 1 **secret detail** (collectible, optional);
- **interaction parts**: hinge/lid (open/close), flip side, wind key, UV layer;
- an optional **uncanny trait**:
  - `hum`: hums, glows gold and rattles its drawer when its owner is at the window, and swells when they speak;
  - `frost`: frost shader, cold breath particles, crackle;
  - `tomorrow`: a future date on the tag or object, ink "still wet" shimmer.

### 4.3 Hotspot discovery
- A magnifier cursor sparkles when it gets close to an undiscovered hotspot (within screen distance, facing the camera). This guides without spoiling.
- Some hotspots only work when visible from the right side (inside a lid, under the base).
- Clicking one: a micro camera push, a ring pulse, the detail text written in handwriting on a card that flies onto the slip, a pen scratch, and paper dust.
- UV hotspots only show with the blue lamp on (from Day 4).

### 4.4 Claims, clues and asking
- **Claims** are typed facts in the commuter's description (`where`, `when`, `contents`, `initials`...). They are written on the slip.
- **Clues** are discovered details. Each has a fact text ("Name strip inside: B. BRAMBLE") and a neutral question ("Anything inside the canopy?").
- **Ask**: click a clue on the slip and the claimant answers. Answers are authored per claimant per detail. Unauthored ones fall back to a short in-character "couldn't say". In two-claimant cases, both answer.
- The game never says "contradiction". You compare, and the Ledger confirms at day's end.

### 4.5 Verdicts and grading
- RETURN (to claimant A or B), REFUSE, or SEAL.
- Every case authors `best` verdicts (the ✓ stamp) and optional `acceptable` ones (the ½ stamp: for example, REFUSE instead of SEAL against Mr Vell).
- Day stamps: 3 if every case is best, 2 with at most one acceptable or wrong, 1 when completed.
- "Thoroughness" is a secondary stat (case details found / total).
- Wrong item: if you put the wrong object on the tray and stamp RETURN, the claimant says "That's not mine" and the case continues (no penalty). The exception is the Grey Gentleman and imposters, who will happily take anything you give them, so be careful.

### 4.6 Consequences
- Refused items remain for their real owner later in the week, which creates callbacks.
- Story flags: `vellItems` (count), `ringFate`, `harrietPhoto`, `penhallowRest`, `edieRecord`, `pipGlobe`, plus others.
- Each item given to Vell desaturates the station by 12% through post-processing colour adjustments. The world visibly loses colour.
- If an item a later case needs is gone, that case plays a short "it's not here" vignette and resolves on its own, without penalising you twice.

### 4.7 The photographs (trailer moment)
Five framed photographs sit on the desk and corkboard. Before the ring:
1. *Lost Property staff, 1921*: young Agnes alone behind this desk, on her first day.
2. *Agnes's retirement, 1962*: old Agnes alone with a cake ("41 YEARS").
3. *Mum and me, 1951*: the clerk as a child at the seaside with Mum.
4. *Platform 9 opening, 1921*: a crowd and a banner.
5. *The Polaroid*: you at this desk, from a camera that shouldn't exist, dated tomorrow (it arrives at the end of Day 1).

When you RETURN the ring to Thomas: the music stops, every photo glows in turn, and the camera pans across the desk as each one cross-dissolves with a ripple. Afterwards: Agnes and Thomas at the desk together; a golden anniversary "41 YEARS" with both of them; Mum and me *with Grandma Agnes and Grandpa Thomas*; Thomas waving on Platform 9; the Polaroid with old Agnes and Thomas standing behind you. One beat later, the next morning, her notes are signed "Agnes Hale".

---

## 5. Story

**Premise.** Ninefold Junction is where nine lines meet. The ninth isn't on any map. Things lost anywhere, at any time, sometimes arrive on Platform 9. Agnes Pell ran Lost Property from 1921 to 1962 and kept the Iron Drawer for things that must never go back. She retired last Friday. You are her replacement, and as the photographs eventually reveal, her grandchild.

**Agnes and Thomas.** On Friday 14 October 1921 Thomas Hale boarded the 9:40 to propose to Agnes, who was waiting on the platform. The ring slipped from his pocket into the ninth line. Thomas never arrived: he has been looking for it ever since, a "stray" between times. Agnes took the job at Lost Property the next Monday and waited 41 years for the ring to turn up. It turns up on your Thursday.

**Mr Vell, the Grey Gentleman.** A polite collector of things that should never be returned. He reads every tag and knows every detail, and nothing ever hums for him. He is gathering the station's keys (the Station Master's backwards watch, the compass, the Iron Drawer itself) to take the ninth line for himself.

### Day by day
- **Monday, "First Shift".** Gentle tutorial. Ordinary items, the first liar, and the humming suitcase: Mrs Marsh's luggage, lost in 1934. Hook: the Polaroid of *you at this desk*, dated tomorrow, arrives on the tray. Gazette: "WOMAN, 94, REUNITED WITH LUGGAGE LOST IN 1934".
- **Tuesday, "Two of Everything".** Twins, a callback for the constable, the Grey Gentleman's first visit, and the first SEAL (a watch found *tomorrow* that runs backwards).
- **Wednesday, "Tomorrow's Photograph".** A young mother claims a photograph of a moment she hasn't lived yet. A WWI soldier comes for his letters, cold. A young man in a 1921 suit asks for a ring that isn't here.
- **Thursday, "The Ring".** UV lamp. Mr Vell has a forged chit. A jazzman's violin hums. A record from next year. Thomas returns and the ring hums. **Every photograph changes.**
- **Friday, "The Last Train".** Mastery combinations. Old Agnes (and maybe Thomas) at the window. Mr Vell's final requisition for the Iron Drawer. The last train, the shutter, and the ending photograph.

### Endings
| Ending | Condition | Final photograph |
|---|---|---|
| **The 9:40** (best) | Ring returned to Thomas, Vell got nothing | The whole week's cast on Platform 9, old Agnes and Thomas in front, you in the middle |
| **The Long Wait** | Ring returned to old Agnes on Friday (or never returned), Vell got nothing | Agnes alone at the window wearing the ring, smiling |
| **Grey Ninefold** | Vell got 2 or more items, or the ring | Vell behind *your* desk, colour drained |

Ending text includes per-flag epilogue lines (Harriet's husband, Penhallow at rest, Edie's band, Pip's Christmas present...).

---

## 6. Content: 25 objects, 24 cases

### 6.1 Objects
| # | Object | Storage | Arrives | Case details (hidden unless noted) | Secret | Trait |
|---|---|---|---|---|---|---|
| 1 | Brown leather wallet | Drawer A | Mon | Opens: photo of a dachshund "Biscuit"; embossed "W.B." under flap; ticket stub Harwick→Ninefold | Pressed four-leaf clover | |
| 2 | Red knitted scarf | Drawer B | Mon | Moth hole near fringe; sewn label "Knitted with love, Nan" | A tiny knitted "C" in the corner | |
| 3 | Green umbrella, duck-head handle | Shelf | Mon | Name strip inside canopy "B. BRAMBLE"; chipped beak | A sweet wrapper tucked in the folds | |
| 4 | Black umbrella | Shelf | Mon | Handle stamp "Ninefold Rly"; after the ring: "T. HALE, Station Master" | Initials scratched on the ferrule "A+T" | |
| 5 | Battered brown suitcase | Shelf | Mon | Latch initials "O.M."; stickers Lisbon 1931, Vienna 1934; inside, a lavender sachet | Dance card from the 1934 Vienna ball | hum |
| 6 | Brass compass | Drawer C | Mon | Needle points to the Iron Drawer, not north; lid engraving "Find what is lost" | Tiny map of the 9 lines inside the lid | |
| 7 | Silver locket | Drawer C | Tue | Ivy pattern (visible); inside, a photo of Gran; folded note "For Cecily, who never loses anything. Gran" | A lock of hair tied with blue thread | |
| 8 | Pocket watch, runs backwards | Drawer D | Tue | Tag: found Platform 9, **Wed** 4:10 pm (tomorrow); hands run backwards; case "Station Master, Ninefold" | Gear engraved "IX" | tomorrow |
| 9 | Toy rabbit "Mr Hops" | Drawer E | Tue | Chewed left ear; name tag sewn on foot "HOPS / OKAFOR" | A button eye replaced with a coat button | |
| 10 | Leather briefcase | Shelf | Tue | Opens: racing form with circled picks, egg-and-cress sandwich; lining initials "D.F." | A winning betting slip, never cashed | |
| 11 | Photograph (couple on platform) | Drawer B | Wed | Flip: developer's stamp **Thu 18 Oct 1962**; station clock in photo at 9:40 | Faint third figure in the background: Thomas | tomorrow |
| 12 | Frosted biscuit tin of letters | Drawer F | Wed | Opens: letters to "Mabel", harmonica; lid "Lt. A. Penhallow, 1917"; frosted | A pressed poppy | frost |
| 13 | Workman's lunch tin | Drawer F | Wed | Opens: corned beef sandwich; note "Ask about the raise! Love, Mavis" | A drawing by a child: "DAD" with a train | |
| 14 | Wedding ring in velvet box | Drawer A | Thu | Tag: Platform 9, 9:40, **14 Oct 1921**; band engraving "A, always the 9:40, T"; ticket stub (UV date 14.X.21) | Pressed forget-me-not under the cushion | hum |
| 15 | Violin case | Shelf | Thu | Opens: violin; label in f-hole "L. Delacroix, New Orleans"; set list taped in lid | A photo of Lou's mother tucked in the lid | hum |
| 16 | Gramophone record in sleeve | Drawer D | Thu | Label "Lou Delacroix & the Ninefold Five, *Last Train Home*"; flip: "Recorded March **1963**" | Sleeve signed "To Edie, my leading lady, L." | tomorrow |
| 17 | Spectacles in case | Drawer E | Fri | Frosted lenses; case "E. Plum, Optician to the Great Western, 1889" | Folded bus timetable from 1903 | frost |
| 18 | Brass birdcage, clockwork canary | Shelf | Fri | Wind key: sings three notes; base plate "A.L." (hidden underneath); door latch | Tiny feather from a real canary | |
| 19 | Snow globe of Ninefold Station | Drawer C | Fri | Shake: snow; base "Souvenir of Ninefold"; UV: "**Fri 21 Dec 1962**" | A tiny figure at the Lost Property window | tomorrow (UV) |
| 20 | Black wallet | Drawer A | Tue | Opens: library card "M. Quill"; empty | IOU from a chess partner | decoy |
| 21 | Travel chess set | Drawer E | Tue | Mid-game position; note "Your move, Arthur. M." | A missing black knight carved from cork | decoy |
| 22 | Seashell | Drawer F | Wed | Hold to ear: the sea, then a station announcement | Sand from a beach that no longer exists | decoy |
| 23 | Hatbox | Shelf | Mon | Opens: feathered hat; theatre stub "Larkspur in *Twelfth Night*" | Love letter, unsent | decoy |
| 24 | Single opera glove | Drawer B | Thu | Embroidered "E.L."; lipstick mark | Pressed rose petal in the finger | decoy |
| 25 | Iron key on a red ribbon | Iron Drawer lid | Mon | The Iron Drawer key; tag "Do not lend. A.P." | Scratched "1921" | (desk object, Vell's final target) |

Decoys add search texture: two wallets, two umbrellas, and similar drawers mean you have to read the tags. Each decoy's secret also links to the story or another character.

### 6.2 Cast
| Character | Silhouette and voice hook | Days |
|---|---|---|
| Gus Penrose, porter | Red cap, big ears, freckles; quick and cheery | Morning intake, every day |
| Walter Bix, baker | Round, flour-dusted shoulders, walrus moustache, flat cap; low and slow | Mon |
| Clementine Rook, typist | Thin, beret, round specs, bob; high and apologetic | Mon |
| Reggie Stokes, spiv | Pencil moustache, wide trilby, chalk-stripe suit, kipper tie; smooth | Mon |
| Mrs Odile Marsh | Tiny, enormous feathered hat, fur stole, cane; warbly | Mon (Fri epilogue) |
| Constable Bramble | Custodian helmet, huge moustache, brass buttons; booming | Tue |
| Rosalind & Cecily Fairweather | Identical twins in mint coats, red vs blue hair ribbon; chime together | Tue |
| Hugo Haversham, accountant | Bald, round specs, bow tie, sweaty; stammers | Tue |
| **Mr Vell, the Grey Gentleman** | Tall, thin, grey bowler, grey gloves, too-wide smile; low with reverse reverb | Tue, Thu, Fri |
| Pip Okafor, schoolgirl | Braids, satchel, blazer, gap tooth; bouncy | Tue, Fri |
| Dora Finch, bookie | Checked coat, flat cap, pencil behind ear; brassy | Wed |
| Harriet Lowe, young mother | Headscarf, cardigan; soft | Wed (Thu vignette) |
| Victor Crane, antiques dealer | Monocle, cravat, velvet jacket, slick hair; nasal | Wed, Fri |
| **Thomas Hale (1921)** | Flat cap, tweed, buttonhole carnation, sepia tint; hopeful | Mon cameo, Wed, Thu, (Fri old) |
| Lt Arthur Penhallow (cold) | WWI peaked cap, khaki, pale blue tint; distant echo | Wed |
| "Big" Sid Mulroney, platelayer | Huge, donkey jacket, wool beanie; very low and jolly | Thu |
| Lou "Sugar" Delacroix, jazz violinist | Pork-pie hat, goatee, sharp suit, shades; sing-song | Thu |
| Vernon Spratt, rival fiddler | Gaunt, greasy hair, shabby tux; whiny | Thu |
| Edie Larkspur, actress | Platinum curls, pillbox hat with veil, fur collar; theatrical | Thu |
| Ezra Plum (cold) | Top hat, mutton chops, frock coat, frost; wheezy | Fri |
| Prof. Ambrose Lark | Owlish specs, tweed, feather in hatband, bushy brows; fussy | Fri |
| Old Agnes (+ old Thomas) | Grey bun, cardigan, specs on a chain; warm | Fri |

### 6.3 Cases
Notation: **R** = RETURN, **✗** = REFUSE, **S** = SEAL. "Lesson" is the difficulty-curve purpose.

**Monday: First Shift** (storage: 1, 2, 3, 4, 5, 6, 23, 25)
| # | Claimant | Item asked for | Truth | Best | Lesson |
|---|---|---|---|---|---|
| 1.1 | Walter Bix | Brown wallet | Owner. Dog photo, "W.B.", Harwick stub | R | **Guided** full loop: bell, drawer, inspect, open, find, stamp |
| 1.2 | Clementine Rook | Red scarf | Owner. Knows the moth hole and Nan's label | R | Unguided search by tag; inspect for details |
| 1.3 | Reggie Stokes | Green duck umbrella | Liar. Says "plain inside"; the strip says B. BRAMBLE | ✗ | **Asking**: liars know only what's visible |
| 1.4 | Mrs Marsh | "A brown case, dear" | Owner, vague on dates; the suitcase **hums** | R | **Hum** overrides a muddled story |

**Tuesday: Two of Everything** (adds 7, 8, 9, 10, 20, 21)
| # | Claimant | Item | Truth | Best | Lesson |
|---|---|---|---|---|---|
| 2.1 | Constable Bramble | Green duck umbrella | Owner. Knows the name strip and chipped beak | R | Callback; refusing yesterday paid off |
| 2.2 | Fairweather twins | Silver locket | Cecily owns it. The note inside names her; Rosalind says it reads "Happy birthday, Rosie" | R→Cecily | **Two claimants**: the object decides |
| 2.3 | Hugo Haversham | Briefcase | Liar. Says "ledgers, Pemberton account"; inside a racing form, a sandwich, "D.F." | ✗ | Contents contradict |
| 2.4 | Mr Vell | Pocket watch | Not his; found *tomorrow*; he describes it perfectly | S (✗ acceptable) | **Grey Gentleman** and **SEAL** introduced |
| 2.5 | Pip Okafor | Toy rabbit | Her brother's. Chewed ear, "OKAFOR" tag | R | Warm palate cleanser; child logic |

**Wednesday: Tomorrow's Photograph** (adds 11, 12, 13, 22)
| # | Claimant | Item | Truth | Best | Lesson |
|---|---|---|---|---|---|
| 3.1 | Dora Finch | Briefcase | Owner. Egg-and-cress, picks, "D.F." | R | Callback |
| 3.2 | Harriet Lowe | Photograph | Real owner, but it's dated **tomorrow** | S | The designer's "photograph taken tomorrow"; emotional pressure against the rule |
| 3.3 | Victor Crane | Frosted tin | Living descendant with a certificate; the tin is **frost** | ✗ | **Frost** rule, living claimant |
| 3.4 | Thomas Hale | "A small velvet box, a ring" | Not in storage (yet) | ✗ | Sometimes it isn't here. Sets up Thursday |
| 3.5 | Lt Penhallow (cold) | Frosted tin | Owner; window frosts; knows "Mabel", the harmonica, 1917 | R | Frost rule, cold claimant (a moving release) |

**Thursday: The Ring** (adds 14, 15, 16, 24). Vignette: Harriet and her husband (flag-dependent).
| # | Claimant | Item | Truth | Best | Lesson |
|---|---|---|---|---|---|
| 4.1 | Big Sid Mulroney | Lunch tin | Owner. Mavis's note | R | Warm-up and humour |
| 4.2 | Mr Vell | Wedding ring | Presents a chit "Hold for Mr V. A.P." UV shows "NEVER TO VELL. A.P." | ✗ (S acceptable, but it locks the ring away) | **UV lamp** intro; emotional manipulation |
| 4.3 | Lou Delacroix vs Vernon Spratt | Violin case | Lou's story is muddled (it was a long night) but the violin **hums** as he speaks; Spratt's story is perfect | R→Lou | Hum overrides story in a two-claimant case |
| 4.4 | Edie Larkspur | Gramophone record | Real owner (a gift from Lou) but it was recorded **1963** | S | Tomorrow can mean next year |
| 4.5 | Thomas Hale | Wedding ring | Owner; the ring **hums**; engraving, 1921 ticket | R | **Every photograph changes.** |

**Friday: The Last Train** (adds 17, 18, 19)
| # | Claimant | Item | Truth | Best | Lesson |
|---|---|---|---|---|---|
| 5.1 | Ezra Plum (cold) | Frosted spectacles | Owner; old line names ("the Great Western") | R | Frost combined with an odd but honest story |
| 5.2 | Prof. Lark vs Victor Crane | Birdcage | Lark knows the hidden base plate and the song; Crane knows only what's visible | R→Lark | Two-claimant mastery |
| 5.3 | Pip Okafor | Snow globe | A gift for her poorly mum; the UV date is **21 Dec** | S | Tomorrow hidden under UV; kindness against the rule |
| 5.4a | Agnes and Thomas, old (if the ring went to Thomas) | Black umbrella, whose strip now reads "T. HALE" | Owner | R | Payoff: the world rewrote itself |
| 5.4b | Old Agnes (otherwise) | The ring (if still in storage) | Owner (the gift was meant for her) | R | Bittersweet "Long Wait" |
| 5.5 | Mr Vell | Requisition for the watch, compass and Iron Drawer key | Forged. UV shows it's dated tomorrow and signed by Vell himself | ✗ | Finale: hold the line |

**Total: 24 cases, 25 objects, 21 commuters + Gus.**

### 6.4 Difficulty curve
- **Mon:** 1 item per claimant, clear tags, guided first case, then one liar and one hum. No penalty for slow play.
- **Tue:** Decoys appear (two wallets, two umbrellas), two claimants, SEAL. Liars are subtler (contents, not names).
- **Wed:** Rules start pulling against sympathy (seal the young mother's photo). "Not here" cases. Frost.
- **Thu:** UV adds a new layer of hidden information. Hum versus a perfect liar. The big emotional choice.
- **Fri:** Combinations of every rule, information hidden under UV, and a boss conversation with Vell.

---

## 7. Art direction

**Look:** stylized realism. Chunky, slightly exaggerated, bevelled forms; rich materials; warm lamplight against a cool blue station dusk. It should read as a handsome miniature diorama, like a toy-maker's version of a 1962 railway office.

**Palette**
| Role | Colour |
|---|---|
| Bottle green (desk leather, lamp shade) | `#1F3B33` |
| Walnut wood | `#4A2E1E` / `#6B4329` |
| Brass | `#C9A15A` |
| Cream paper | `#EFE4CC` |
| Ink navy | `#1E2440` |
| Oxblood (REFUSE, ribbons) | `#7A2222` |
| RETURN green | `#2F6B3A` |
| Iron | `#2B2B2E` |
| Station dusk | `#2C4A57` |
| Lamp amber | `#FFB86B` |
| Hum gold | `#FFD27A` |
| Frost cyan | `#BFE6F2` |
| Ninth-line violet (rare) | `#6E5AA8` |

**Shapes:** rounded rectangles, generous bevels, soft silhouettes, everything a touch oversized and hand-made. Characters have big heads, compact bodies, strong hats, simple glossy eyes, and expressive brows and moustaches.

**Lighting:** a banker's-lamp key light (warm, soft shadows) with a fake volumetric cone and dust motes; cool fill through the window; concourse practicals as bokeh behind the glass; rim light on the commuter from the concourse.

**Camera:** first-person seated, about 50° FOV, breathing sway, eased yaw turns. Inspect mode uses depth of field and a lean-in.

**Post-processing:** ACES tonemapping, warm/teal split toning, bloom (lamp, brass glints), vignette, subtle film grain, SSAO, depth of field (inspect), a desaturation channel driven by `vellItems`.

**Materials:** a small library of shared URP Lit materials with procedural tiling textures generated in Python (wood grain, leather, felt, paper fibre, brushed brass, fabric weave, frosted glass). Blender material slot names map to them (`wood_*`, `leather_*`, `brass`, `paper`, `cloth_*`, `glass`), with per-part tint from the slot name's hex code.

**Typography (OFL/Apache):** Caveat (handwriting: tags, slip), Kalam (Agnes's notes), Special Elite (typewritten forms), Courier Prime (printer receipts), IM Fell English (titles, Gazette), Limelight (station signage), Crimson Pro (UI body).

---

## 8. Audio direction

All audio is synthesized in Python/numpy (`Tools/audio/`) as layered, enveloped WAVs. There are no samples.

- **Ambience:** concourse murmur (shaped noise plus formant babble crowds), distant footsteps, a PA chime with a muffled formant "announcement", periodic train arrivals (low rumble, brake squeal, steam hiss), the clock tick, rain on Friday evening.
- **Desk foley:** drawer slide (filtered noise and wood creak), stop thunk, item pick/put by material (wood knock, metal clink, paper rustle, leather flap, glass tick), latch click, hinge creak, stamp (lift rattle, ink-pad squelch, *thump*), printer (stepper chatter, ratchet, bell, tear), counter bell (inharmonic partials), pen scratch, page turn, lamp click and UV buzz, Iron Drawer (heavy scrape, lock clunk, chain rattle), calendar flip.
- **Uncanny:** hum (warm major chord with slow beating and a sub), frost (crystalline shimmer and creak), tomorrow (reverse swell), photo change (glass-harmonica swell, shimmer, reverse cymbal).
- **Voices:** per-character formant babble (pitch, speed, vowel set, breathiness, vibrato), Animal-Crossing-style but warmer and quieter. Vell's has a reverse reverb; the cold ones have long echo.
- **Music:** a 9-note "Ninefold" motif. Tracks: *Title* (music box), *Day* (gentle jazz waltz: FM e-piano, plucked bass, brushed snare), *Uncanny* (celesta and drone, crossfaded in during hum/frost cases), *Ledger* (short brass and piano cadence), *The Ring* (strings pad swell), *Finale/Credits*. Music ducks under dialogue and big stingers.
- **Mix:** an AudioMixer with Music, SFX, Voice and Ambience groups, plus a low-pass snapshot during menus and pause.

---

## 9. UI, UX and controls

| Input | Action |
|---|---|
| Mouse move | Hover (highlights, tag reveal) |
| Left click | Open/close drawer, pick up item, discover detail, press bell, pick up/use stamp, click clue to ask |
| Left drag (inspect) | Rotate the item (with inertia) |
| Scroll (inspect) | Zoom |
| Right click / Esc (inspect) | Put the item back on the desk mat |
| A / D, ← / → | Turn to cabinet / counter / shelf |
| L | Toggle Agnes's blue lamp (from Day 4) |
| Space | Ring the bell / advance dialogue |
| T | Put the held item on the counter tray |
| Tab | Raise the claim slip for reading |
| Esc | Pause |

- **Claim slip** is a diegetic paper on the desk, mirrored in a readable screen-space clipboard that rises on hover/Tab. It has a claims section, a clues section (click to ask), and a stamp area (two halves in two-claimant cases).
- **Dialogue** appears as a typewriter strip near the commuter, with key claims underlined. Click or Space to advance; the last line stays up.
- **Tag reading:** hovering a tag swings it up and a zoomed tag card appears near the cursor.
- **Onboarding:** Agnes's notes (one or two lines, taped where they apply) and soft pulsing glows on the next thing to click, only during case 1.1. No walls of text, no modal tutorials.
- **Menus:** *Title* (dusk concourse, shutter half-down, luggage-tag menu: Continue / New Week / Days / Curios / Settings / Quit); *Pause* (the ledger opens: Resume / Rules / Settings / Title); *Settings* (Master, Music, SFX, Voice and Ambience volumes; mouse sensitivity; text speed; fullscreen and resolution; quality; screen shake; reduce motion); *Days* (a calendar with stamps per day); *Curios* (a grid of the 25 objects with secrets found).
- **Save:** JSON in `persistentDataPath` for the current day, story flags, per-day best stamps, discovered details and secrets. Settings go in PlayerPrefs. Autosave at day start and day end.

---

## 10. Game feel and juice

- Drawers: spring easing with a slight overshoot, a wood thunk with random pitch, contents jiggle on stop, label card wobble.
- Hover: soft rim highlight, subtle lift, cursor change (hand / magnifier / stamp).
- Pick-up: the item arcs to your hands with ease-out-back; the depth-of-field focus pulls in; a whoosh.
- Rotation: inertia, damping and a soft clamp on zoom.
- Discovery: micro push-in, gold ring pulse, ink-written text, the card flies to the slip, a pen scratch, paper motes.
- Stamp: lift-and-tilt on pickup, a ghost imprint preview on hover, a slam with squash, screen shake (small), an ink imprint at a random angle with splatter, a *thump* with a low sub-boom.
- Printer: lever, chatter, receipt unfurls with curl, tear, flies to the spike; the spike stack grows.
- Commuter: walk-in bob, idle breathing, talk bob synced to babble, blink, emote poses (happy hop, angry shake, sad slump, gasp), and a leave walk.
- Hum: drawer rattles, gold motes, a spatialized chord from the item's direction, slight camera micro-shake, window glow.
- Frost: frost creeps over the window glass from the edges, the breath plume disappears, the lamp flickers.
- Photographs: per-photo dissolve with a radial ripple, sparkle and a chord. The camera pans across the desk.
- Day transitions: the shutter comes down with a rattle, the calendar page flips, a fade through warm white.
- Ledger: check marks stamped one by one with rising pitch, and the Gazette spins in like a newsreel.

---

## 11. Code architecture

Everything is built at runtime from code and data (the pattern packthetrunk proved). `Main.unity` holds only a bootstrap object, so there is no fragile scene wiring.

```
Assets/Game/
  Scripts/
    Core/        Pure C# with no UnityEngine (except JsonUtility):
                 Content model (ObjectDef, DetailDef, CommuterDef, CaseDef, DayDef), ContentDb,
                 RulesEngine (derives the verdict from information), CaseResult, StoryState,
                 SaveData. Unit-tested.
    Flow/        GameDirector (state machine: Title → DayIntro → Case loop → DayLedger → Ending),
                 CaseRunner, TutorialDirector, StoryFlags, ConsequenceApplier
    Desk/        DeskBuilder (spawns env from FBX), CameraRig (yaw views + inspect), Interactable,
                 Drawer, StorageSlot, ItemView (model, tag, hotspots, parts), InspectController,
                 Hotspot, Stamp, ClaimSlip, TicketPrinter, Bell, Lamp (UV), IronDrawer,
                 PhotoFrame, DeskCalendar, CounterTray, ReceiptSpike
    People/      CommuterView (model parts and procedural animation), CommuterDirector (walk in/out),
                 VoiceBabble (runtime granular synthesis from voice WAV grains), WindowFX (frost/hum)
    UI/          UIRoot (runtime-built uGUI + TMP), DialogueStrip, SlipPanel, TagCard, Tooltip,
                 TitleScreen, PauseMenu, SettingsMenu, DaySelect, CurioLedger, DayLedgerView,
                 GazetteView, EndingView, Fader
    Audio/       AudioDirector (SFX bank, pooled sources, music crossfade, ambience, ducking)
    FX/          Tween (tiny tween lib), Shake, Juice helpers, ParticlesFactory, PostFX (Volume control)
    Debug/       AutoPilot (plays the whole week via real interactions, screenshots), DevConsole
  Editor/        BuildScript (Linux), ProjectSetup (URP renderer and volume profile), ModelPostprocessor,
                 ContentValidatorMenu
  Resources/     Content/*.json, Models/*.fbx, Textures/, Audio/, Music/, Fonts/, Photos/, Materials/
  Tests/EditMode ContentTests (schema, references), SolverTests (every case solvable from info)
```

**Data:** `Resources/Content/objects.json`, `commuters.json`, `days.json` (cases, dialogue, answers, outcomes, flags) and `strings.json` (Agnes's notes, Gazette, endings). Loaded with JsonUtility into `[Serializable]` classes. Arrays only, no dictionaries.

**Rules engine:** given a case and the info the player *could* discover (tag, details, answers, traits, the rules unlocked by that day), compute the best verdict. Used by (a) EditMode tests and `Tools/validate_content.py`, which assert that the derived verdict equals the authored verdict for every case under every reachable story state; (b) the AutoPilot; and (c) a debug overlay.

**Input:** the Input System package, read directly from `Mouse.current` / `Keyboard.current` for simplicity.

---

## 12. Asset list (Blender, all scripted in `ArtSource/`)

`ArtSource/lib/laf.py` holds shared helpers: bevelled boxes, rounded cylinders, lathe profiles, text-to-mesh engravings, material-slot naming, FBX export with Unity axes, and preview renders. `ArtSource/build_*.py` holds one builder per asset with a `--only` filter and `--preview` renders, and `ArtSource/contact_sheet.py` makes review sheets.

- **Booth/desk** (`build_desk.py`): desk (walnut, green leather inlay, brass edge), drawer cabinet (body + 6 drawer objects with handles and label holders), shelf unit, Iron Drawer (body, drawer, lock), counter window (frame, glass, speaking grille, tray), wall panelling, banker's lamp (base, shade, switch), ticket printer (body, lever, paper slot), three stamps and ink pad, counter bell (base, dome, plunger), slip clipboard, receipt spike, flip calendar, five photo frames, tea cup and saucer, inkwell and pen, ledger book, coat hook with coat, radiator, wall clock, poster frames.
- **Concourse** (`build_station.py`): arches, big station clock, split-flap departure board, benches, lamp posts, platform sign "9", the back wall, all low detail (it is seen blurred).
- **Objects** (`build_objects.py`): all 25, each with named parts for animation (`Lid`, `Latch`, `Key`, `Door`, `Flap`) and named empties for hotspots (`HS_<detailId>`) so the data and the model line up. Tag attach point `TAG`.
- **Characters** (`build_people.py`): a modular generator (body shape, head shape, nose, brows, eyes, moustache/beard, hair, hat, collar/tie/scarf, accessories). Exports split into `Body`, `Head`, `Eyes`, `Brows`, `Mouth`, `HandL`, `HandR` for procedural animation. 23 configurations.
- **Photographs** (`render_photos.py`): Eevee renders of posed characters in mini-sets, sepia/colour graded in the compositor. 5 before plus 5 after, plus ending photographs.

Textures (`Tools/textures/gen_textures.py`): tiling albedo/normal/roughness for wood, leather, felt, paper, brass, cloth and frost; the paper and tag backgrounds; the stamp imprints; the UI paper.

---

## 13. Milestones

| M | Goal | Exit criteria |
|---|---|---|
| **M0** | Plan, scaffold, tooling | PLAN.md, git, URP project, venv, fonts, editor launch verified |
| **M1** | **Core prototype** | Greybox desk with camera views, drawers, pick-up and inspect with rotate/zoom, hotspots, claim slip, ask, stamps, printer, one commuter, three test cases. Iterate with screenshots until it feels great |
| **M2** | Art pipeline | Blender library; desk/booth, 6 objects and 3 characters built, rendered and imported; texture and audio generators |
| **M3** | Full content | All 25 objects, 22 characters, 24 cases, photographs; validator and EditMode tests green |
| **M4** | Flow and UI | Title, day intro, ledger, Gazette, endings, pause, settings, days, curios, save/load, onboarding |
| **M5** | Polish | Juice list, particles, post-processing, music and audio mix, transitions, frost/hum/UV FX, photo-change sequence |
| **M6** | Verify and ship | AutoPilot full-week PASS, console clean, screenshots reviewed, Linux build in `Builds/`, README |

Commit at each milestone, and more often inside them.

---

## 14. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Procedural models look cheap up close in inspect mode | Bevels and subdivision, material library with real normal/roughness, contact-sheet review in Blender before import, hero lighting in inspect |
| 22 characters is a lot of art | Modular generator with strong per-character silhouette parameters; seen chest-up only; procedural animation, no rigs |
| Text-heavy deduction feels like homework | Short lines, highlighted claims, voice babble, diegetic notes; the 3D inspection carries discovery |
| Brute-forcing "Ask" | The game never confirms; the Ledger reveals results only at day's end |
| Unity editor/player on Wayland and a shared machine | Batch-mode builds, `-force-wayland` player, in-game screenshot capture, AutoPilot instead of manual driving, close editors |
| Scope | Core feel first, then content breadth, then polish; every content item is data, so polish isn't blocked by content |
| Story paradoxes | Keep it dreamlike; the photographs show rather than explain |

---

## 15. The 5-minute prototype test

A first-time player, within five minutes, must:
1. Ring the bell and meet Walter Bix, a round baker with a walrus moustache and a low rumbling voice.
2. Open drawer A and *feel* it slide (sound, overshoot, rattle).
3. Turn the wallet in their hands, open it, and find the photo of Biscuit the dachshund (a gold ring pulse and a handwritten clue).
4. Stamp RETURN: thump, the printer chatters, Walter beams.
5. Catch Reggie Stokes lying: ask "Anything inside the canopy?", he says "Plain, I think", and the strip says B. BRAMBLE.
6. Hear and see the suitcase on the shelf **hum** when tiny Mrs Marsh shuffles up, and watch her open it.
7. See the Ledger stamp their week's first check marks, then the Polaroid of *themselves at this desk, dated tomorrow*, slides onto the tray.

**Pass condition:** they ask "what happens tomorrow?" or click "Next Day" without being prompted. If drawers don't feel good, or the hum or the liar don't land, iterate before building more content.
