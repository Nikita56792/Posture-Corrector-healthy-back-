import speech_recognition as sr
import webbrowser
import pyttsx3
import openai
from translate import Translator

# ключ API от OpenAI
openai.api_key = ""

def recognize_speech():
    recognizer = sr.Recognizer()

    with sr.Microphone() as source:
        print("Скажите что-то:")
        recognizer.adjust_for_ambient_noise(source)
        audio = recognizer.listen(source)

    try:
        recognized_text = recognizer.recognize_google(audio, language="ru-RU")
        print("Вы сказали:", recognized_text)

        # Добавляем проверку на фразу "привет siri"
        if "привет siri" in recognized_text.lower():
            print("Привет! Чем могу помочь?")
            speak("Привет! Чем могу помочь?")
        elif "открой браузер" in recognized_text.lower():
            webbrowser.open("chrome")  # Может потребоваться указать полный путь к исполняемому файлу Chrome
            speak("Запускаю!")
        else:
            gpt_response = generate_gpt_response(recognized_text)
            print("GPT-3.5 ответ:", gpt_response)

            # Проверка языка ответа GPT-3.5
            if detect_language(gpt_response) == 'en':
                translated_response = translate_text(gpt_response, 'ru')
                print("Переведенный ответ:", translated_response)
                speak(translated_response)
            else:
                speak(gpt_response)

    except sr.UnknownValueError:
        print("Извините, не удалось распознать речь")
    except sr.RequestError as e:
        print(f"Ошибка при запросе к сервису распознавания: {e}")

def speak(text):
    engine = pyttsx3.init()
    engine.say(text)
    engine.runAndWait()

def generate_gpt_response(prompt):
    response = openai.Completion.create(
        engine="text-davinci-002",  # Выберите подходящий движок
        prompt=prompt,
        max_tokens=100
    )
    return response['choices'][0]['text']

def detect_language(text):
    # В данном примере проверяем только на английский, но можно расширить функционал
    return 'en' if ' '.join(text.split()[:5]).isascii() else 'other'

def translate_text(text, target_language):
    translator = Translator(to_lang=target_language)
    translation = translator.translate(text)
    return translation

if __name__ == "__main__":
    while True:
        recognize_speech()
