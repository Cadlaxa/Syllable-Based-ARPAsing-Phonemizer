# Syllable-Based ARPAsing Phonemizer
Custom English Arpabet Phonemizer based on Syllable-Based API Phonemizer
 
~~This Phonemizer was optimized for **`Arpasing 0.1.0`** and **`Arpasing 0.2.0`** banks **(the original list, Kanru Hua made)** made for [Openutau](https://www.openutau.com/). This Phonemizer works with other ARPAsing banks with CC, VV, CV, and VC fallback plus constant uniformed phoneme lengths.~~

As of version **`V0.0.55`** and above, it now supports the majority of all ARPAsing voicebanks, also **`V0.0.55`** and above is much more stable than the previous versions of this external Phonemizer.

#### 📍 if you want to suggest a feature for ARPA+, you can suggest on the issues tab or the discussions tab
#### 📍 if there's any issue with the Phonemizer, you can contact me through my [Twitter](https://twitter.com/cadlaxa). Let me know if there's a problem tehee.
 - - - -
### Table of contents
- **[How to download and install the custom Phonemizer](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#how-to-download-and-install-the-custom-phonemizer)**
- **[Supported Consonants and Vowels](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#how-to-download-and-install-the-custom-phonemizer)**
- **[Mechanics of the Phonemizer](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#mechanics-of-the-phonemizer)**
    - **[How syllables work](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#how-syllables-work)**
    - **[Vowel and Consonant Fallbacks](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#vowel-and-consonant-fallbacks)**
    - **[Phonemizer Demo](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#phonemizer-demo)**
 - - - -
## How to download and install the custom Phonemizer

- To download and install the Phonemizer, click on Releases then click the dll file to download. When downloaded, move ArpaPlusPhonemizer.dll into the path\to\OpenUtau\Plugins folder or just simply drag the dll file and drop onto the OpenUtau program.
 - - - -
 ## Supported Consonants and Vowels
 - **Consonants are automatically supported by this phonemizer but if the consonants aren't listed on the `GetTransitionBasicLengthMs`, their lengths are defaulted to 1.0 ms**
 - **Currently supported vowels:**
   - **`aa`, `ax`, `ae`, `ah`, `ao`, `aw`, `ay`, `eh`, `er`, `ey`, `ih`, `iy`, `ow`, `oy`, `uh`, `uw`, `a`, `e`, `i`, `o`, `u`, `ai`, `ei`, `oi`, `au`, `ou`, `ix`, `ux`,
`aar`, `ar`, `axr`, `aer`, `ahr`, `aor`, `or`, `awr`, `aur`, `ayr`, `air`, `ehr`, `eyr`, `eir`, `ihr`, `iyr`, `ir`, `owr`, `our`, `oyr`, `oir`, `uhr`, `uwr`, `ur`,
`aal`, `al`, `axl`, `ael`, `ahl`, `aol`, `ol`, `awl`, `aul`, `ayl`, `ail`, `ehl`, `el`, `eyl`, `eil`, `ihl`, `iyl`, `il`, `owl`, `oul`, `oyl`, `oil`, `uhl`, `uwl`, `ul`,
`naan`, `an`, `axn`, `aen`, `ahn`, `aon`, `on`, `awn`, `aun`, `ayn`, `ain`, `ehn`, `en`, `eyn`, `ein`, `ihn`, `iyn`, `in`, `own`, `oun`, `oyn`, `oin`, `uhn`, `uwn`, `un`,
`aang`, `ang`, `axng`, `aeng`, `ahng`, `aong`, `ong`, `awng`, `aung`, `ayng`, `aing`, `ehng`, `eng`, `eyng`, `eing`, `ihng`, `iyng`, `ing`, `owng`, `oung`, `oyng`, `oing`, `uhng`, `uwng`, `ung`, `aam`, `am`, `axm`, `aem`, `ahm`, `aom`, `om`, `awm`, `aum`, `aym`, `aim`, `ehm`, `em`, `eym`, `eim`, `ihm`, `iym`, `im`, `owm`, `oum`, `oym`, `oim`, `uhm`, `uwm`, `um`, `oh`, `eu`, `oe`, `yw`, `yx`, `wx`**
    - **📍 note: if the custom vowels are not here on the list or in the code, they will be recognized as consonants, Syllable-based Phonemizers will have to define all possible vowels in order to them to be recognized as a Vowel**
 - - - -
 ## Mechanics of the Phonemizer

### How syllables work
- **Syllables are built like:**

  - Starting C: `[- c]`
  - Starting CV: `[- c v]` or `[- cv]`
  - Starting V: `[- v]` or `[v]`
  - VV: `(Fallbacks to [v c][c v]/[cv] then [c v]/[cv] then finally [v])`
  - Connecting CV: `[c v]` (Fallbacks to `[cv]`if no alias is detected)
  - Connecting VC: `[v c]` then `[v -]` `[- c`
  - Connecting CC: `[c c]` (with consonant fallbacks) then `[c1 -]` `[- c2]`
  - Ending C: `[c -]`
  - Ending V: `[v -]`

- **Phoneme length are specified directly to the phonemizer:**
  - Default transition in ms: '1.0'
  - Vowels: 'default'
  - Consonants: '1.3'
  - Affricates: '1.5'
  - Long Consonants: '2.3'
  - Semi-long Consonants: '1.3'
  - Tap Consonant: '0.5'
  - Glide Consonants: '2.5'
 - - - -
### Vowel and Consonant Fallbacks
- **This custom Phonemizer supports vowel and consonant fallbacks:**
  - CV Fallback: `[c v]` or `[cv]`
  - VV Fallback: `(Fallbacks to [v c][c v]/[cv] then [c v]/[cv] then finally [v])`
  - Connecting VC Fallback: `[v c]` then `[v -]` `[- c]`
  - Connecting CC: `[c c]` (with consonant fallbacks) then `[c1 -]` `[- c2]`
  - Starting and Ending Consonants
 - - - -
### Phonemizer Demo
- **Consonant Timing Difference**
  - **Vocal: Arpa_test**
  - `EN ARPA`: **consonant timings are constant to 0.5 ms**
    
https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/9b6556c8-3c80-4a9d-8b83-c44432abdc8b
  - `EN ARPA+`: **consonant timings changes depending on the consonant**
    
https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/0fbf752b-3265-443b-95c2-a1beb34d331e
- **English**
  - **Vocal: KYE ARPAsing**
  - `EN ARPA`:
    
     https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/ee107d2b-16b5-4847-abab-79d676a261be
  - `EN ARPA+`:
    
    https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/0ef5a490-d522-4434-90c7-cec71778dc65
- **Chinese (with dictionary)**
  - **Vocal: LIEE ENGLISH by @utauraptor**
  - `EN ARPA`:
    
    https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/e1dd8dcf-f79a-4f79-aef5-355f51bf953a
  - `EN ARPA+`:
    
    https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/93bb4429-3aaf-4e5f-b553-55403cb5257b
- **Japanese (with dictionary)**
  - **Vocal: Ryujin Soru by @Sora/Rippa**
  - `EN ARPA`:
    
    https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/65842485-3487-49ec-8391-d0a67504a4a5
  - `EN ARPA+`:
    
    https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/8b8ebc7f-3e5e-488d-a1bd-cba27ce3b8b6

