# Syllavle-Based ARPAsing Phonemizer
Custom English Arpabet Phonemizer based on Syllable-Based API Phonemizer
 
This Phonemizer was optimizer for `Arpasing 0.1.0` and `Arpasing 0.2.0` **banks the original list Kanru Hua** made for [Openutau](https://www.openutau.com/).
 - - - -
#### 📍 if there's any issue on the Phonemizer, you can contact me through my [Twitter](https://twitter.com/cadlaxa). Let me know if there's a problem tehee.
 - - - -
### Table of contents
- **[How to download and install the custom Phonemizer](https://github.com/Cadlaxa/Openutau-Yaml-Dictionaries#how-to-download-and-install-dictionaries-for-openutau)**
- **[Mechanics of the Phonemizer](https://github.com/Cadlaxa/Openutau-Yaml-Dictionaries#how-to-use-the-dictionary--the-suffix-support)**
    - **[How syllables work](https://github.com/Cadlaxa/Openutau-Yaml-Dictionaries#japanese-dictionary-usage)**
    - **[Vowel and Consonant Fallbacks](https://github.com/Cadlaxa/Openutau-Yaml-Dictionaries#korean-dictionary-usage)**
    - **[Phonemizer Demo](https://github.com/Cadlaxa/Openutau-Yaml-Dictionaries#chinese-dictionary-usage)**
 - - - -
## How to download and install the custom Phonemizer

- To download and install the Phonemizer, click on Tags then download the zip file. When downloaded, move ArpaPlusPhonemizer.dll into the path\to\OpenUtau\Plugins folder or just simply drag the dll file and drop onto the OpenUtau program.
 - - - -
 ## Mechanics of the Phonemizer

### How syllables work
**Syllables are built like this:**

- Starting C: [- c]
- Starting V: [- v]
- VV: (with V and CV fallbacks if there's so VV available on the bank)
- Connecting CV: [c v]
- Connecting VC: [v c]
- Connecting CC: [c c] (with consonant fallbacks)
Batchim (syllable finals): [v C] (required);
- Ending C: [c -]
- Ending V: [v -]
- Fallback phonemes: [dx, zh, dr, tr]
 - - - -
### Vowel and Consonant Fallbacks
- - - -
### Phonemizer Demo
