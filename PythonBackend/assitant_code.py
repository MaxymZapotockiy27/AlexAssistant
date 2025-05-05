import whisper
import sounddevice as sd
from scipy.io.wavfile import write
import time
import threading
import queue
import webbrowser
import os
import re
import requests
import urllib3
import datetime
import urllib.parse
import numpy as np
from gtts import gTTS
from playsound import playsound
import pygame
import pyautogui
import platform
import subprocess
import webbrowser
import grpc
import json
import logging
import traceback
try:
    import control_pb2
    import control_pb2_grpc
    import assistant_bridge_pb2
    import assistant_bridge_pb2_grpc

except ImportError as e:
    print(f"[ERROR] Failed to import generated gRPC modules in assistant_code: {e}")

windows_volume_control_available = False
if platform.system() == "Windows":
    try:
        from pycaw.pycaw import AudioUtilities, IAudioEndpointVolume
        from comtypes import CLSCTX_ALL
        from ctypes import cast, POINTER
        windows_volume_control_available = True
    except ImportError:
        print("--------------------------------------------------------------------------")
        print("WARNING: pycaw library not found.")
        print("         Volume control on Windows requires pycaw.")
        print("         Install it using: pip install pycaw")
        print("--------------------------------------------------------------------------")
output_filename = "Assistant/output_speech.mp3"
os.makedirs(os.path.dirname(output_filename), exist_ok=True)

urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

class ContinuousTranscription:
    API_URL = "https://localhost:7022/WeatherForecast"   
    ACTIVATION_PHRASES = [
        "hello alex", 
        "hi alex", 
        "hey alex", 
        "alex", 
        "привіт alex", 
        "алекс", 
        "ало alex"
    ]
    DEACTIVATION_PHRASES = [
        "thank you",
        "thank you alex",
        "thanks alex",
        "thanks you",
        "bye alex",
        "goodbye alex",
        "до побачення alex", 
        "дякую alex",       

 
        "that's all alex",
        "that will be all alex",
        "we are done alex",
        "i'm done alex",
        "stop listening alex",
        "go to sleep alex", 
        "dismissed alex",
        "exit alex",
        "quit alex",
        "shutdown alex", 
        "okay bye alex",
        "alright bye alex",
        "see you later alex",
        "talk to you later alex"
       
    ]

    def get_grpc_port():
        try:
           
            current_dir = os.path.dirname(os.path.abspath(__file__))
            project_root = os.path.abspath(os.path.join(current_dir, '..'))
            port_file = os.path.join(project_root, 'servercsharp.txt')
            with open(port_file, "r") as f:
                port = f.read().strip()
                return port
        except Exception as e:
            print(f"[❌] Could not read gRPC port from file: {e}")
            return None
    def find_device_index_by_name(self, target_name):
     
        print(f"[SYSTEM] Searching for input microphone containing name: '{target_name}'")
        devices = sd.query_devices()
        found_device = None

        available_input_devices = []
        for i, device in enumerate(devices):
          
            is_input = device.get('max_input_channels', 0) > 0
          
            is_likely_output_mapper = 'mapper' in device.get('name', '').lower() and not is_input

            if is_input and not is_likely_output_mapper:
                 available_input_devices.append({'index': i, 'name': device['name']})
               
                 if target_name.lower() in device.get('name', '').lower():
                    found_device = {'index': i, 'name': device['name']}
                    print(f"[SYSTEM] Found matching device: Index={found_device['index']}, Name='{found_device['name']}'")
                    break 

        if not found_device:
            print(f"[ERROR] No input device found containing name: '{target_name}'")
            print("[SYSTEM] Available input devices:")
            if available_input_devices:
                for dev in available_input_devices:
                    print(f"  - Index {dev['index']}: {dev['name']}")
            else:
                print("  No input devices detected by sounddevice.")
            return None 

        return found_device['index']
    def safe_remove_file(self, filename):
        """Safely attempts to remove a file, handling potential errors."""
        if filename and os.path.exists(filename):
            try:
                os.remove(filename)
             
            except PermissionError:
                print(f"[WARNING] Could not remove file {filename}. It might still be in use.")
            except OSError as e:
                print(f"[ERROR] Error removing file {filename}: {e}")

    def speak(self, text):
        global output_filename
        try:
            self.is_speaking = True
            print(f"[TTS] Speaking: {text}")
            tts = gTTS(text=text, lang=self.language, slow=False)
            tts.save(output_filename)
            sound = pygame.mixer.Sound(output_filename)
            sound.play()
        
            while pygame.mixer.get_busy(): 
                time.sleep(0.1)
            time.sleep(0.5) 

        except Exception as e:
            print(f"[ERROR] Text-to-speech failed: {e}")
        finally:
            self.is_speaking = False 
            self.safe_remove_file(output_filename)
    def load_commands(self):
        json_path = os.path.join(
            os.environ.get("APPDATA"), 
            "AlexAssistant", 
            "commands.json"
        )        
        try:
            with open(json_path, "r") as file:
                self.commands = json.load(file)
        except FileNotFoundError:
            print(f"[ERROR] File not found: {json_path}")
        except json.JSONDecodeError:
            print(f"[ERROR] JSON decoding error in file: {json_path}")
        except Exception as e:
            print(f"[ERROR] Unexpected error: {e}")

    def load_commands_if_modified(self):
       
        json_path = os.path.join(os.environ.get("APPDATA"), "AlexAssistant", "commands.json")
    
        try:
            if os.path.exists(json_path):
                current_mtime = os.path.getmtime(json_path)
                if current_mtime > self.last_commands_mtime:
                    print(f"[SYSTEM] Commands file modified, reloading...")
                    with open(json_path, "r") as file:
                        self.commands = json.load(file)
                    self.last_commands_mtime = current_mtime
                    return True
            return False
        except Exception as e:
            print(f"[ERROR] Failed to check/load commands: {e}")
            return False
    def __init__(self, sample_rate=16000, language="en"):
        self.fs = sample_rate
        self.language = language
        self.audio_queue = queue.Queue()
        self.stop_event = threading.Event()
        self.command_active = False
        self.command_buffer = ""  
        self.is_speaking = False  
        self.commands = []
        self.last_commands_mtime = 0
        self.load_commands() 
        pygame.mixer.init()

        port = ContinuousTranscription.get_grpc_port()
        if not port:
            print("[ERROR] Cannot initialize gRPC connection without a port.")
            self.stub = None 
        else:
            try:
                channel = grpc.insecure_channel(f"localhost:{port}")
                stub = assistant_bridge_pb2_grpc.AssistantBridgeStub(channel)
                self.stub = stub 
                print(f"[gRPC] Stub initialized for connection to localhost:{port}")
            except NameError as e:
                 print(f"[ERROR] Failed to initialize gRPC stub. Is 'assistant_bridge_pb2_grpc' imported? Details: {e}")
                 self.stub = None 
            except Exception as e:
                 print(f"[ERROR] Unexpected error during gRPC channel/stub initialization: {e}")
                 self.stub = None 

        
        
       
        self.listen_time = 3 
        self.command_listen_time = 7
        self.silence_threshold = 0.01 
        self.silence_frames = 10  
        
        print("[SYSTEM] Loading Whisper model...")
        self.whisper_model = whisper.load_model("base")
        print("[SYSTEM] Model loaded successfully.")

    def put_windows_to_sleep(self):
        """Puts the Windows computer to sleep after confirmation."""
        print("Confirmed. Putting the computer to sleep")
       
       
        try:
            os.system("rundll32.exe powrprof.dll,SetSuspendState 0,1,0")
        except Exception as e:
            print(f"[ERROR] Failed to put Windows to sleep: {e}")
            self.speak("Sorry, I encountered an error trying to enter sleep mode.")

   
    def set_microphone_boost(self, device_id, gain_factor=2.0):
            self.mic_gain = gain_factor
            print(f"[SYSTEM] Microphone gain set to {gain_factor}x")
            return True


    def validate_city(self, city):
        """
        Validate if a city exists by calling the GetCity gRPC method.

        Args:
            city (str): City name to validate.

        Returns:
            str: The validated city name (potentially formatted by the server)
                 if found, otherwise an empty string.
        """
 
        if not self.stub:
            print("[ERROR] City validation skipped: gRPC stub is not available.")
            return "" 

        print(f"[CITY_VALIDATION] Validating city via gRPC: {city}")
        try:
            request = assistant_bridge_pb2.CityRequest(city=city)
            response = self.stub.GetCity(request)

           
            if response and response.fullCityName:
                print(f"[CITY_VALIDATION] City '{city}' validated as '{response.fullCityName}'")
                return response.fullCityName 
            else:
                print(f"[CITY_VALIDATION] City '{city}' not found by gRPC service.")
                return "" 

        except grpc.RpcError as e:
            print(f"[ERROR] gRPC call GetCity failed for '{city}': {e.code()} - {e.details()}")
            return ""
        except Exception as e:
            print(f"[CRITICAL] Unexpected error during city validation for '{city}': {e}")
            return "" 

    def choose_microphone(self):
        devices = sd.query_devices()
        print("[SYSTEM] Available microphones:")
        input_devices = [i for i, device in enumerate(devices) if device['max_input_channels'] > 0]
        
        for i in input_devices:
            print(f"{i}: {sd.query_devices()[i]['name']}")

        while True:
            try:
                choice = int(input("[SYSTEM] Enter microphone number for recording: "))
                if choice in input_devices:
                    return choice
                else:
                    print("[ERROR] Invalid microphone number. Try again.")
            except ValueError:
                print("[ERROR] Please enter an integer.")

    def record_audio_with_vad(self, device):
    
        while not self.stop_event.is_set():
           
            if self.is_speaking:
              
                print("[RECORDING] Recording paused. Assistant is speaking...")
                time.sleep(0.5)
                continue
                
            listen_duration = self.command_listen_time if self.command_active else self.listen_time
            print(f"[RECORDING] Listening for {'commands' if self.command_active else 'activation'} (max {listen_duration}s)...")
            
            frames = []
            silent_frames = 0
            voice_detected = False
            
            
            try:
                with sd.InputStream(samplerate=self.fs, channels=1, device=device, callback=None) as stream:
                    start_time = time.time()
                    
                    while time.time() - start_time < listen_duration and not self.stop_event.is_set():
                       
                        if self.is_speaking:
                            print("[RECORDING] Recording interrupted. Assistant started speaking.")
                            break
                            
                 
                        frame, overflowed = stream.read(int(self.fs * 0.1))  
                        frames.append(frame.copy())
                        
                      
                        volume = np.abs(frame).mean()
                        
                        if volume > self.silence_threshold:
                            voice_detected = True
                            silent_frames = 0
                        elif voice_detected:
                            silent_frames += 1
                            
                     
                        if voice_detected and silent_frames >= self.silence_frames:
                            print("[RECORDING] Silence detected, stopping recording.")
                            break
            except Exception as e:
                print(f"[ERROR] Recording error: {e}")
                time.sleep(1)
                continue
            
        
            if voice_detected and frames and not self.is_speaking:
                audio_data = np.concatenate(frames)
                temp_file = f"temp_recording_{time.time()}.wav"
                write(temp_file, self.fs, audio_data)
                print(f"[RECORDING] Recording saved, duration: {len(audio_data)/self.fs:.2f}s")
                self.audio_queue.put(temp_file)
            else:
                print("[RECORDING] No voice detected or recording interrupted, skipping...")

    def is_phrase_in_text(self, text, phrase_list):
        text = text.lower().strip()
        if isinstance(phrase_list, str):
            phrase_list = [phrase_list]
        
        for phrase in phrase_list:
            if re.search(r'\b' + re.escape(phrase.lower()) + r'\b', text):
                return True
        return False

    def is_exact_phrase(self, text, phrase_list):
   
        text = text.lower().strip()
        text = re.sub(r'[^\w\s]', '', text) 
        if isinstance(phrase_list, str):
            phrase_list = [phrase_list]

        for phrase in phrase_list:
            phrase = phrase.lower().strip()
            phrase = re.sub(r'[^\w\s]', '', phrase) 
            if text == phrase:
                return True
        return False

    def extract_city_from_weather_query(self, transcription):
        """
        Extract city name from weather-related queries with API validation.
        
        Args:
            transcription (str): The transcribed text to extract city from.
        
        Returns:
            str or None: Extracted and validated city name or None if no valid city found.
        """
        if not transcription or not isinstance(transcription, str):
            print("[DEBUG] Invalid transcription input")
            return None
        transcription = transcription.lower().strip()

    
        city_extraction_patterns = [
            r'weather in\s+([\w\s]+)',
            r'temperature in\s+([\w\s]+)',
            r'what(?:\'s| is) the weather in\s+([\w\s]+)',
            r'how(?:\'s| is) the weather in\s+([\w\s]+)',
            r'tell me (?:the )?weather in\s+([\w\s]+)'
        ]


        for pattern in city_extraction_patterns:
            match = re.search(pattern, transcription)
            if match:
                city = match.group(1).strip().title()
                
     
                if self.validate_city(city):
                    print(f"[DEBUG] City validated: {city}")
                    return city

        print("[DEBUG] No valid city found in transcription")
        return None

    def get_weather(self, city):
        """
        Fetch weather data for a given city using the GetWeather gRPC method.

        Args:
            city (str): Validated city name.

        Returns:
            assistant_bridge_pb2.WeatherResponse or None: Weather data object if successful,
                                                         None otherwise.
        """
      
        if not self.stub:
            print("[ERROR] Get weather skipped: gRPC stub is not available.")
            self.speak("Sorry, I'm disconnected from my knowledge base right now.") 
            return None

        print(f"[WEATHER] Attempting to retrieve weather via gRPC for: {city}")
        try:
            request = assistant_bridge_pb2.CityRequest(city=city)
            response = self.stub.GetWeather(request)

            if response:
               
                if response.city or response.description:
                    print(f"[WEATHER] gRPC data retrieved for {response.city}: Temp={response.temperature}, Desc={response.description}")
                    return response 
                else:
                    print(f"[ERROR] gRPC GetWeather returned empty data for city: {city}")
                    return None
            else:
                
                print(f"[ERROR] No response object received from gRPC GetWeather for city: {city}")
                return None

        except grpc.RpcError as e:
            print(f"[ERROR] gRPC call GetWeather failed for '{city}': {e.code()} - {e.details()}")
            return None
        except Exception as e:
            print(f"[CRITICAL] Unexpected error retrieving weather via gRPC for '{city}': {e}")
            return None

    def send_to_gemini(self, query):
   
        if not self.stub:
             print("[ERROR] Gemini request skipped: gRPC stub is not available.")
             self.speak("Sorry, I'm disconnected from my knowledge base right now.")
             return

  
        if not query or len(query.strip()) < 3:
            print("[GEMINI] Query too short or empty. Skipping request.")
            return

        try:
            print(f"[GEMINI] Sending query to AI: {query}")
            request = assistant_bridge_pb2.GeminiRequest(message=query)

            response = self.stub.GeminiQues(request)

           
            if response and response.responses:
                first_response = response.responses[0] 
                print(f"[GEMINI] Response: {first_response}")
                self.speak(first_response)
        
            elif response:
                 print("[ERROR] Received response from Gemini, but the 'responses' list is empty.")
                 self.speak("Sorry, I received an empty response.")
            else:
                print("[ERROR] No response object received from Gemini API call.")
                self.speak("Sorry, I couldn't process that request.")

        except grpc.RpcError as e:
            print(f"[ERROR] Gemini API request failed: {e.code()} - {e.details()}")
            self.speak("Sorry, I couldn't connect to my knowledge base.")
        except AttributeError as e:
            print(f"[CRITICAL] Error accessing response attribute: {e}")
            self.speak("Sorry, there was an error processing the response structure.")
        except Exception as e:
            print(f"[CRITICAL] Gemini processing error: {type(e).__name__} - {e}") 
            self.speak("Sorry, there was an unexpected error processing your request.")
            
    def process_command(self, transcription):
        """
        Process voice commands with improved handling of multiple commands.
        
        Args:
            transcription (str): The transcribed command text.
        """
        if not self.command_active:
            return

        self.load_commands_if_modified()

        try:
  
            transcription = transcription.lower().strip()
            
           
            
            print(f"[COMMAND] Processing: {transcription}")
            if self.is_phrase_in_text(transcription, ["what time is it", "what's the time", "tell me the time", "what is the time"]):
                now = datetime.datetime.now()
                time_str = now.strftime("%H:%M")
                self.speak(f"The current time is {time_str}")
                return

            if self.is_phrase_in_text(transcription, ["turn on sleep mode", "sleep mode", "sleep"]):
                if self.show_confirmation_dialog():
                    self.put_windows_to_sleep()
                    self.speak("Sleep Mode Activated")
                else:
                    print("Was canceled")
                    self.speak("Sleep Mode was canceled")
                return
                        
            if self.is_phrase_in_text(transcription, "open browser"):
                webbrowser.open('https://www.google.com')
                self.speak("Opening web browser.")
                return
                
            if self.is_phrase_in_text(transcription, "open calculator"):
                try:
                    subprocess.Popen("calc", shell=True)
                    print("Opening calculator")
                    self.speak("Opening calculator")
                except FileNotFoundError:
                    print("Error: Could not find command 'calc'. Check that calculator is available.")
                except Exception as e:
                    print(f"[ERROR]: {e}")
                return 
                
            if self.is_phrase_in_text(transcription, "open visual studio"):
                try:
                    subprocess.Popen("code", shell=True)
                    print("Opening Visual Studio Code")
                    self.speak("Opening Visual Studio Code")
                except FileNotFoundError:
                    print("Error: Could not find command 'Visual studio'. Check that Visual Studio is available.")
                except Exception as e:
                    print(f"[ERROR]: {e}")
                return                       
                
            if self.is_exact_phrase(transcription, "open youtube"):
                webbrowser.open('https://www.youtube.com/')
                self.speak("Opening Youtube")
                return
                
            if self.is_phrase_in_text(transcription, "open twitch"):
                webbrowser.open('https://www.twitch.tv/')
                self.speak("Opening Twitch")
                return
                
            if self.is_phrase_in_text(transcription, "open spotify"):
                webbrowser.open('https://open.spotify.com')
                self.speak("Opening Spotify")
                return 
                
            if self.is_exact_phrase(transcription, "open youtube music"):
                webbrowser.open('https://music.youtube.com')
                self.speak("Opening YouTube Music")
                return                                                  
                
            if self.is_phrase_in_text(transcription, "volume up") or self.is_phrase_in_text(transcription, "increase volume"):
                if self._change_system_volume("up"):
                    self.speak("Volume increased.")
                return
                    
            if self.is_phrase_in_text(transcription, "volume down") or self.is_phrase_in_text(transcription, "decrease volume"):
                if self._change_system_volume("down"):
                    self.speak("Volume decreased.")
                return     
                
            if self.is_phrase_in_text(transcription, ["mute", "silence", "sound off"]):
                if self._change_system_volume("mute"):
                    self.speak("Audio muted.")
                return
                    
            if self.is_phrase_in_text(transcription, ["unmute", "sound on"]):
                if self._change_system_volume("unmute"):
                    self.speak("Audio unmuted.")
                return

            if self.is_phrase_in_text(transcription, "stop"):
                try:
                    pyautogui.press('playpause') 
                except Exception as e:
                    print(f"[ERROR]: {e}")
                self.speak("Ok")
                return 
                
            if self.is_phrase_in_text(transcription, "play"):
                try:
                    pyautogui.press('playpause') 
                except Exception as e:
                    print(f"[ERROR]: {e}")
                self.speak("Ok")
                return
                
            if self.is_phrase_in_text(transcription, "next track"):
                try:
                    pyautogui.press('nexttrack') 
                except Exception as e:
                    print(f"[ERROR]: {e}")
                self.speak("Ok")
                return
                
            if self.is_phrase_in_text(transcription, "previous track"):
                try:
                    pyautogui.press('prevtrack')
                    pyautogui.press('prevtrack')  
                except Exception as e:
                    print(f"[ERROR]: {e}")
                self.speak("Ok")
                return                           
                
            if self.is_phrase_in_text(transcription, "open file manager"):
                try:
                    if os.name == 'nt': 
                        os.startfile('explorer.exe')
                    elif os.name == 'posix': 
                        os.system('xdg-open ~')
                    self.speak("Opening file manager.")
                    return
                except OSError as e:
                    print(f"[ERROR] File manager open failed: {e}")
                    return


            self.load_commands_if_modified()
            for command in self.commands:
                if command["Phrase"].lower() in transcription:
                   
                    if command["ActionType"] == 0:
                        webbrowser.open(command["ActionTarget"])
                        print(f"Opening URL: {command['ActionTarget']}")
                    elif command["ActionType"] == 1: 
                        try:
                            subprocess.Popen(command["ActionTarget"], shell=True)
                            print(f"Executing file: {command['ActionTarget']}")
                        except Exception as e:
                            print(f"[ERROR] Failed to execute file: {e}")
                    return
            
        
            city = self.extract_city_from_weather_query(transcription)
            
            if city:
              
                weather_data = self.get_weather(city) 

                if weather_data: 
                 
                    temp = weather_data.temperature  
                    desc = weather_data.description  
                    response_city = weather_data.city if weather_data.city else city 

                 
                    temp_str = f"{temp:.1f} degrees Celsius" 

              
                    tts_text = f"The weather in {response_city} is currently {temp_str} with {desc}."
                    print(f"[DEBUG] Prepared TTS text: {tts_text}") 

                    self.speak(tts_text) 

                else:
               
                    self.speak(f"Sorry, I couldn't get the weather information for {city} right now.")
                return 
                    
        
           
                
            if len(transcription.strip().split()) >= 2:
                self.send_to_gemini(transcription)
            else:
                print(f"[COMMAND] Command too short: '{transcription}'")

        except Exception as e:
            print(f"[CRITICAL] Command processing error: {e}")


    def start_with_device(self, mic_name_target):
        """
        Starts the assistant's recording and transcription threads using the specified microphone
        and blocks until these threads complete or an error occurs.
        """
        logger = logging.getLogger(__name__) 
        logger.info(f"Attempting to start assistant with mic name target: '{mic_name_target}'")

    
        self.stop_event.clear() 
        self.command_active = False
        self.is_speaking = False
        self.record_thread = None 
        self.transcribe_thread = None

       
        logger.debug("Clearing audio queue before starting...")
        while not self.audio_queue.empty():
            try:
                stale_file = self.audio_queue.get_nowait()
                self.safe_remove_file(stale_file)
            except queue.Empty:
                break
            except Exception as e:
                logger.warning(f"Error clearing stale audio file from queue: {e}")

        try:
            selected_device_index = self.find_device_index_by_name(mic_name_target)
            if selected_device_index is None:
                raise ValueError(f"Microphone '{mic_name_target}' not found or is not an input device.")

            
            logger.debug(f"Creating recording and transcription threads for device index {selected_device_index}.")
            self.record_thread = threading.Thread(
                target=self.record_audio_with_vad,
                args=(selected_device_index,),
                daemon=True,
                name="RecordThread"
            )
            self.transcribe_thread = threading.Thread(
                target=self.transcribe_audio,
                daemon=True,
                name="TranscribeThread"
            )

            
            self.record_thread.start()
            logger.info(f"Recording thread ({self.record_thread.name}) started.")
            self.transcribe_thread.start()
            logger.info(f"Transcription thread ({self.transcribe_thread.name}) started.")

           
            try:
                mic_info = sd.query_devices(selected_device_index)['name']
                logger.info(f"Assistant threads started successfully using microphone: {mic_info} (Index: {selected_device_index})")
            except Exception as query_ex:
                logger.warning(f"Could not query device name after starting threads: {query_ex}")
                logger.info(f"Assistant threads started successfully using microphone Index: {selected_device_index}")


           
            logger.info("start_with_device is now waiting for assistant threads to join...")

          
            if self.transcribe_thread:
                self.transcribe_thread.join() 
                logger.info(f"Transcription thread ({self.transcribe_thread.name}) has joined.")

            if self.record_thread:
                self.record_thread.join() 
                logger.info(f"Recording thread ({self.record_thread.name}) has joined.")

            logger.info("Both assistant threads have joined. start_with_device will now finish.")

        except sd.PortAudioError as pae:
            logger.error(f"[ERROR] PortAudio error during startup in start_with_device: {pae}")
           
            raise RuntimeError(f"Audio device error: {pae}") from pae
        except ValueError as ve: 
            logger.error(f"[ERROR] Value error during startup in start_with_device: {ve}")
            raise ve 
        except Exception as e:
            logger.exception(f"[CRITICAL] Unexpected error during assistant thread startup or joining in start_with_device:")
           
            raise RuntimeError(f"Failed to start or run assistant threads: {e}") from e
        finally:
           
            logger.info("Exiting start_with_device method (finally block).")
       
            self.record_thread = None
            self.transcribe_thread = None


        try:
            selected_device_index = self.find_device_index_by_name(mic_name_target)

            if selected_device_index is None:
                raise ValueError(f"Microphone '{mic_name_target}' not found or is not an input device.")

           
            self.record_thread = threading.Thread(target=self.record_audio_with_vad, args=(selected_device_index,), daemon=True)
            self.transcribe_thread = threading.Thread(target=self.transcribe_audio, daemon=True)

            self.record_thread.start()
            self.transcribe_thread.start()

            print(f"[SYSTEM] Assistant started successfully using microphone: {sd.query_devices(selected_device_index)['name']} (Index: {selected_device_index})")
           
        except sd.PortAudioError as pae:
             print(f"[ERROR] PortAudio error during startup: {pae}")
             print("[ERROR] This might indicate an issue with the selected audio device or sound system configuration.")
             raise RuntimeError(f"Audio device error: {pae}") from pae
        except Exception as e:
            print(f"[CRITICAL] Failed to start assistant threads: {e}")
      
            raise RuntimeError(f"Failed to start assistant: {e}") from e

    def stop(self):
       
        if not self.stop_event.is_set():
            print("[SYSTEM] Initiating assistant shutdown sequence...")
            logger = logging.getLogger(__name__) 
            self.stop_event.set() 

          
            try:
                 print("[SYSTEM] Putting None into audio queue for transcription thread.")
                 self.audio_queue.put(None, block=False, timeout=1) 
            except queue.Full:
                 logger.warning("[WARN] Audio queue is full during shutdown signal. Transcription thread might take longer to stop.")
            except Exception as e:
                 logger.error(f"[ERROR] Failed to put None into queue during stop: {e}")


            thread_timeout = 5

           
            if hasattr(self, 'transcribe_thread') and self.transcribe_thread and self.transcribe_thread.is_alive():
                logger.info(f"Waiting for transcription thread ({self.transcribe_thread.name}) to terminate...")
                self.transcribe_thread.join(timeout=thread_timeout * 2)
                if self.transcribe_thread.is_alive():
                     logger.warning(f"Transcription thread did not terminate within {thread_timeout * 2} seconds.")
                else:
                     logger.info("Transcription thread terminated.")
            else:
                logger.info("Transcription thread was not running or not found.")


      
            if hasattr(self, 'record_thread') and self.record_thread and self.record_thread.is_alive():
                logger.info(f"Waiting for recording thread ({self.record_thread.name}) to terminate...")
                self.record_thread.join(timeout=thread_timeout)
                if self.record_thread.is_alive():
                     logger.warning(f"Recording thread did not terminate within {thread_timeout} seconds.")
                else:
                     logger.info("Recording thread terminated.")
            else:
                 logger.info("Recording thread was not running or not found.")


          
            self.command_active = False
            print("[SYSTEM] Assistant stop sequence completed in ContinuousTranscription.stop().")
        else:
             print("[SYSTEM] Stop requested, but assistant stop_event was already set.")
    def transcribe_audio(self):
        logger = logging.getLogger(__name__)
        logger.info("Transcription thread started.")
        audio_file = None
        while not self.stop_event.is_set():
            try:
                if self.is_speaking:
                    time.sleep(0.1)
                    continue

                audio_file = None
                try:
                    audio_file = self.audio_queue.get(timeout=1)
                    if audio_file is None:
                        logger.info("Received None from queue, stopping transcription loop.")
                        break
                except queue.Empty:
                    continue

                if self.stop_event.is_set():
                    logger.info("Stop event detected before transcription, discarding audio.")
                    if audio_file: self.safe_remove_file(audio_file)
                    break

                logger.info(f"Processing audio file: {audio_file}...")
                start_time = time.time()
                result = self.whisper_model.transcribe(audio_file, language=self.language)
                duration = time.time() - start_time
                logger.info(f"Whisper transcription finished in {duration:.2f} seconds.")
                transcription = result.get("text", "")

                confidence = result.get("segments", [{}])[0].get("no_speech_prob", 1.0)

                if not transcription or not transcription.strip() or confidence > 0.8:
                    logger.info(f"Low quality transcription (confidence: {1-confidence:.2f}). Skipping.")
                    self.safe_remove_file(audio_file)
                    audio_file = None
                    continue

                logger.info(f"[TRANSCRIPTION] Text: {transcription} (confidence: {1-confidence:.2f})")

                should_speak = False 
                should_activate_form = False
                should_deactivate_form = False 

                if self.is_exact_phrase(transcription, self.ACTIVATION_PHRASES):
                    self.command_active = True
                    should_activate_form = True
                    should_speak = True
                    tts_text = "Hello. How can I help you"
                    logger.info("[SYSTEM] Assistant activated. Ready for commands.")

                elif  self.command_active and  self.is_exact_phrase(transcription, self.DEACTIVATION_PHRASES):
                    self.command_active = False
                    should_deactivate_form = True
                    should_speak = True
                    tts_text = "Goodbye!"
                    logger.info("[SYSTEM] Assistant deactivated.")

                if should_activate_form:
                     if self.stub:
                        logger.debug("[gRPC] Activating form on assistant activation...")
                        try:
                            request = assistant_bridge_pb2.ActivateFormRequest(activate=True)
                            response = self.stub.ActivateForm(request)
                            logger.debug(f"[gRPC] ActivateForm(True) response: Success={response.success}, Msg='{response.message}'")
                        except grpc.RpcError as e:
                            logger.error(f"[ERROR] gRPC call ActivateForm(True) failed: {e.code()} - {e.details()}")
                        except Exception as e:
                            logger.error(f"[ERROR] Unexpected error during ActivateForm(True): {e}")
                     else:
                        logger.warning("[WARN] gRPC stub not available, skipping form activation.")

                if should_deactivate_form:
                    if self.stub:
                        logger.debug("[gRPC] Deactivating form on assistant deactivation...")
                        try:
                            request = assistant_bridge_pb2.ActivateFormRequest(activate=False)
                            response = self.stub.ActivateForm(request)
                            logger.debug(f"[gRPC] ActivateForm(False) response: Success={response.success}, Msg='{response.message}'")
                        except grpc.RpcError as e:
                            logger.error(f"[ERROR] gRPC call ActivateForm(False) failed: {e.code()} - {e.details()}")
                        except Exception as e:
                            logger.error(f"[ERROR] Unexpected error during ActivateForm(False): {e}")
                    else:
                        logger.warning("[WARN] gRPC stub not available, skipping form deactivation.")

                if should_speak:
                    logger.info(f"Attempting to speak: {tts_text}")
                    self.speak(tts_text)
                    logger.info("Finished speaking.")
                    self.safe_remove_file(audio_file)
                    audio_file = None
                    continue

                if self.command_active:
                    logger.info(f"Processing command: {transcription}")
                    self.process_command(transcription)
                    logger.info("Finished processing command.")

                if audio_file:
                    logger.debug(f"Attempting to remove audio file: {audio_file}")
                    self.safe_remove_file(audio_file)
                    audio_file = None

            except Exception as e:
                logger.exception(f"CRITICAL error in transcription loop:")
                if audio_file:
                    logger.warning(f"Attempting to remove potentially problematic audio file: {audio_file}")
                    self.safe_remove_file(audio_file)
                    audio_file = None
            finally:
                 if self.stop_event.is_set():
                     logger.info("Stop event detected at end of transcription loop iteration.")
                     break

        logger.info("Transcription thread finished.")

    def start(self):
        try:
            selected_device = self.choose_microphone()
            self.set_microphone_boost(selected_device, gain_factor=2.5)
            record_thread = threading.Thread(target=self.record_audio_with_vad, args=(selected_device,))
            transcribe_thread = threading.Thread(target=self.transcribe_audio)
            
            record_thread.start()
            transcribe_thread.start()
            
            print("[SYSTEM] Voice assistant started. Say 'Hello Alex' to activate.")
            print("[SYSTEM] Press Enter to stop...\n")
            input()
            
            self.stop_event.set()
            record_thread.join()
            transcribe_thread.join()
            
        except Exception as e:
            print(f"[CRITICAL] An error occurred: {e}")

    def safe_remove_file(self, filename):
        try:
            if os.path.exists(filename):
                os.remove(filename)
        except OSError as e:
            print(f"[ERROR] Could not remove file {filename}: {e}")
    def _change_system_volume(self, direction, amount=5):
        """
        Changes the system master volume or mutes/unmutes.
        :param direction: 'up', 'down', 'mute', 'unmute'
        :param amount: Percentage to change volume by (ignored for mute/unmute)
        :return: True if successful, False otherwise
        """
        system = platform.system()
        try:
            if system == "Windows":
                if not windows_volume_control_available:
                    self.speak("Sorry, I cannot control volume on Windows without the pycaw library.")
                    return False

                devices = AudioUtilities.GetSpeakers()
                interface = devices.Activate(IAudioEndpointVolume._iid_, CLSCTX_ALL, None)
                volume = cast(interface, POINTER(IAudioEndpointVolume))

                if direction == "up":
                    current_volume_scalar = volume.GetMasterVolumeLevelScalar()
                    new_volume_scalar = min(1.0, current_volume_scalar + (amount / 100.0))
                    volume.SetMasterVolumeLevelScalar(new_volume_scalar, None)
                
                    if volume.GetMute():
                         volume.SetMute(0, None)
                elif direction == "down":
                    current_volume_scalar = volume.GetMasterVolumeLevelScalar()
                    new_volume_scalar = max(0.0, current_volume_scalar - (amount / 100.0))
                    volume.SetMasterVolumeLevelScalar(new_volume_scalar, None)
                elif direction == "mute":
                    volume.SetMute(1, None) 
                elif direction == "unmute":
                    volume.SetMute(0, None) 
                else:
                    print(f"Unknown volume direction: {direction}")
                    return False
                return True

            elif system == "Darwin":
                if direction == "up":
                    current_vol_cmd = "output volume of (get volume settings)"
                    current_vol = int(subprocess.check_output(['osascript', '-e', current_vol_cmd]).strip())
                    new_vol = min(100, current_vol + amount)
                    set_vol_cmd = f"set volume output volume {new_vol}"
                    subprocess.run(['osascript', '-e', set_vol_cmd], check=True)
                    is_muted_cmd = "output muted of (get volume settings)"
                    is_muted = subprocess.check_output(['osascript', '-e', is_muted_cmd]).strip().decode() == 'true'
                    if is_muted:
                         subprocess.run(['osascript', '-e', "set volume output muted false"], check=True)
                elif direction == "down":
                    current_vol_cmd = "output volume of (get volume settings)"
                    current_vol = int(subprocess.check_output(['osascript', '-e', current_vol_cmd]).strip())
                    new_vol = max(0, current_vol - amount)
                    set_vol_cmd = f"set volume output volume {new_vol}"
                    subprocess.run(['osascript', '-e', set_vol_cmd], check=True)
                elif direction == "mute":
                    subprocess.run(['osascript', '-e', "set volume output muted true"], check=True)
                elif direction == "unmute":
                     subprocess.run(['osascript', '-e', "set volume output muted false"], check=True)
                else:
                    print(f"Unknown volume direction: {direction}")
                    return False
                return True

            elif system == "Linux":
               
                control_name = 'Master' 
                amixer_ok = False
                try:
                    cmd_base = ['amixer', '-q', '-M', 'set', control_name]
                    if direction == "up":
                        subprocess.run(cmd_base + [f'{amount}%+'], check=True)
                      
                        subprocess.run(['amixer', '-q', 'set', control_name, 'unmute'], check=True) 
                    elif direction == "down":
                        subprocess.run(cmd_base + [f'{amount}%-'], check=True)
                    elif direction == "mute":
                        subprocess.run(['amixer', '-q', 'set', control_name, 'mute'], check=True)
                    elif direction == "unmute":
                        subprocess.run(['amixer', '-q', 'set', control_name, 'unmute'], check=True)
                    else:
                         print(f"Unknown volume direction: {direction}")
                         return False
                    amixer_ok = True 
                except (FileNotFoundError, subprocess.CalledProcessError) as e_amixer_master:
             
                    print(f"INFO: amixer failed for 'Master': {e_amixer_master}. Trying 'PCM'.")
                    control_name = 'PCM'
                    try:
                        cmd_base = ['amixer', '-q', '-M', 'set', control_name]
                        if direction == "up":
                           subprocess.run(cmd_base + [f'{amount}%+'], check=True)
                           subprocess.run(['amixer', '-q', 'set', control_name, 'unmute'], check=True) 
                        elif direction == "down":
                           subprocess.run(cmd_base + [f'{amount}%-'], check=True)
                        elif direction == "mute":
                           subprocess.run(['amixer', '-q', 'set', control_name, 'mute'], check=True)
                        elif direction == "unmute":
                           subprocess.run(['amixer', '-q', 'set', control_name, 'unmute'], check=True)
                        else:
                           print(f"Unknown volume direction: {direction}")
                           return False
                        amixer_ok = True 
                    except (FileNotFoundError, subprocess.CalledProcessError) as e_amixer_pcm:
                        print(f"INFO: amixer also failed for 'PCM': {e_amixer_pcm}. Trying pactl.")
                        amixer_ok = False 

            
                if not amixer_ok:
                    try:
                        cmd_base = ['pactl']
                        sink = '@DEFAULT_SINK@'
                        if direction == "up":
                            subprocess.run(cmd_base + ['set-sink-volume', sink, f'+{amount}%'], check=True)
                        
                            subprocess.run(cmd_base + ['set-sink-mute', sink, '0'], check=True) 
                        elif direction == "down":
                            subprocess.run(cmd_base + ['set-sink-volume', sink, f'-{amount}%'], check=True)
                        elif direction == "mute":
                            subprocess.run(cmd_base + ['set-sink-mute', sink, '1'], check=True) 
                        elif direction == "unmute":
                            subprocess.run(cmd_base + ['set-sink-mute', sink, '0'], check=True) 
                        else:
                            print(f"Unknown volume direction: {direction}")
                            return False
                    except (FileNotFoundError, subprocess.CalledProcessError) as e_pactl:
                        print(f"ERROR: pactl also failed: {e_pactl}")
                        self.speak("Sorry, I couldn't find a way to control volume on this Linux system.")
                        return False 

                return True 

            else:
                self.speak(f"Sorry, volume control is not supported on {system} yet.")
                return False

        except FileNotFoundError as e:
           
             missing_cmd = e.filename
             self.speak(f"Error: Required command '{missing_cmd}' not found.")
             return False
        except subprocess.CalledProcessError as e:
            self.speak(f"Error changing volume: Command failed.")
            print(f"Command error details: {e}")
            return False
        except Exception as e:
            self.speak("An unexpected error occurred while changing volume.")
            print(f"Unexpected volume error: {e}")
            return False
        

    

     
if __name__ == "__main__":
    transcriber = ContinuousTranscription()
    transcriber.start()