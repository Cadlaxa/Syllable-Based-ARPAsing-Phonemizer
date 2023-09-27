# Syllable-Based ARPAsing Phonemizer
Custom English Arpabet Phonemizer based on Syllable-Based API Phonemizer
 
~~This Phonemizer was optimized for **`Arpasing 0.1.0`** and **`Arpasing 0.2.0`** banks **(the original list, Kanru Hua made)** made for [Openutau](https://www.openutau.com/). This Phonemizer works with other ARPAsing banks with CC, VV, CV, and VC fallback plus constant uniformed phoneme lengths.~~

As of version **`V0.0.55`** and above, it now supports the majority of all ARPAsing voicebanks, also **`V0.0.55`** and above is much more stable than the previous versions of this external Phonemizer.

#### 📍 if you want to suggest a feature for ARPA+, you can suggest on the issues tab
#### 📍 if there's any issue with the Phonemizer, you can contact me through my [Twitter](https://twitter.com/cadlaxa). Let me know if there's a problem tehee.
 - - - -
### Table of contents
- **[How to download and install the custom Phonemizer](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#how-to-download-and-install-the-custom-phonemizer)**
- **[Mechanics of the Phonemizer](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#mechanics-of-the-phonemizer)**
    - **[How syllables work](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#how-syllables-work)**
    - **[Vowel and Consonant Fallbacks](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#vowel-and-consonant-fallbacks)**
    - **[Phonemizer Demo](https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/blob/main/README.md#phonemizer-demo)**
 - - - -
## How to download and install the custom Phonemizer

- To download and install the Phonemizer, click on Releases then click the dll file to download. When downloaded, move ArpaPlusPhonemizer.dll into the path\to\OpenUtau\Plugins folder or just simply drag the dll file and drop onto the OpenUtau program.
 - - - -
 ## Mechanics of the Phonemizer

### How syllables work
- **Syllables are built like:**

  - Starting C: `[- c]`
  - Starting CV: `[- c v]` or `[- cv]`
  - Starting V: `[- v]` or `[v]`
  - VV: `(Fallbacks to [V C][C V]/[CV] then [C V]/[CV] then finally [V])`
  - Connecting CV: `[c v]` (Fallbacks to `[CV]`if no alias is detected)
  - Connecting VC: `[v c]`
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
  - CV Fallback: `[c v]` or `[CV]`
  - VV Fallback: `(Fallbacks to [V C][C V]/[CV] then [C V]/[CV] then finally [V])`
  - Connecting VC Fallback: `[v c]`
  - Connecting CC: `[c c]` (with consonant fallbacks) then `[c1 -]` `[- c2]`
  - Starting and Ending Consonants
 - - - -
### Phonemizer Demo
- **English**
  - **Vocal: KYE by @Winter_drivE**
  - `EN ARPA`:
    
     https://github.com/Cadlaxa/Syllable-Based-ARPAsing-Phonemizer/assets/92255161/ee107d2b-16b5-4847-abab-79d676a261be
  - `EN ARPA+`:
  - 
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

