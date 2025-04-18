import sys
from TTS.api import TTS

# Init TTS
tts = TTS(model_name="tts_models/en/ljspeech/tacotron2-DDC", progress_bar=False, gpu=False)

text = sys.argv[1]
output_path = sys.argv[2]

# Generate speech
tts.tts_to_file(text=text, file_path=output_path)
