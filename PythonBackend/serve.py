import os
import sys
import socket
import grpc
import threading
import time
import logging
from datetime import datetime
from concurrent import futures
import pygame
current_dir = os.path.dirname(os.path.abspath(__file__))
log_filename = f"serve_py_{datetime.now().strftime('%Y%m%d_%H%M%S')}.log"
log_filepath = os.path.join(current_dir, log_filename)
log_level = logging.DEBUG

logger = logging.getLogger(__name__)
logger.setLevel(log_level)

file_handler = logging.FileHandler(log_filepath, mode='w', encoding='utf-8')
file_handler.setLevel(log_level)
formatter = logging.Formatter('%(asctime)s - %(levelname)s - [%(threadName)s:%(funcName)s:%(lineno)d] - %(message)s')
file_handler.setFormatter(formatter)
logger.addHandler(file_handler)

logger.info("-----------------------------------------------------")
logger.info("serve.py execution started")
logger.info(f"Python Executable: {sys.executable}")
logger.info(f"Python Version: {sys.version}")
logger.info(f"Script Path (current_dir): {current_dir}")
logger.info(f"Log File: {log_filepath}")
logger.info(f"Current Working Directory: {os.getcwd()}")
logger.info(f"Sys Path: {sys.path}")
logger.info(f"PYTHONPATH environment variable: {os.environ.get('PYTHONPATH')}")
logger.info(f"Arguments: {sys.argv}")

if current_dir not in sys.path:
    logger.info(f"Appending script directory to sys.path: {current_dir}")
    sys.path.append(current_dir)
else:
    logger.debug(f"Script directory already in sys.path: {current_dir}")

try:
    logger.debug("Importing required modules...")
    import control_pb2
    import control_pb2_grpc
    from control_pb2 import Status
    from assitant_code import ContinuousTranscription
    logger.info("Core modules imported successfully.")
except ImportError as e:
    logger.exception("CRITICAL: Failed to import required modules!")
    print(f"FATAL_ERROR: Failed to import modules. Check serve_py log. Error: {e}", file=sys.stderr)
    sys.stderr.flush()
    sys.exit(f"ImportError: {e}")
except Exception as e:
    logger.exception("CRITICAL: Unexpected error during initial imports!")
    print(f"FATAL_ERROR: Unexpected error during imports. Check serve_py log. Error: {e}", file=sys.stderr)
    sys.stderr.flush()
    sys.exit(f"ImportError: {e}")


class ControlServicer(control_pb2_grpc.ControlServiceServicer):
    def __init__(self, transcriber):
        logger.debug("ControlServicer initializing...")
        self.transcriber = transcriber
        self.is_running = False
        self._stop_event = threading.Event()
        self.assistant_thread = None
        logger.debug("ControlServicer initialized.")

    def TurnOn(self, request, context):
        mic_name_target = request.mic_name_target if request else "Default (request was null)"
        logger.info(f"TurnOn request received. Mic target: '{mic_name_target}'. Current is_running state: {self.is_running}")
        try:
            if not self.is_running:
                logger.info("Assistant is not running. Preparing to start...")
                self._stop_event.clear()
                self.assistant_thread = threading.Thread(
                    target=self._start_assistant,
                    args=(mic_name_target, self._stop_event),
                    name="AssistantThread"
                )
                self.assistant_thread.daemon = True
                self.assistant_thread.start()
                self.is_running = True
                logger.info(f"Assistant thread start initiated. Set is_running to {self.is_running}.")
                return control_pb2.Status(message="Assistant start initiated successfully")
            else:
                logger.warning("TurnOn request received, but assistant is already running or starting.")
                return control_pb2.Status(message="Assistant is already running")
        except Exception as e:
            logger.exception("Error processing TurnOn request:")
            self.is_running = False
            logger.error(f"TurnOn failed. Reset is_running to {self.is_running}.")
            return control_pb2.Status(message=f"Error starting assistant: {str(e)}")

    def TurnOff(self, request, context):
        logger.info(f"TurnOff request received. Current is_running state: {self.is_running}")
        try:
            if self.is_running:
                logger.info("Assistant is marked as running. Attempting to stop...")
                self._stop_event.set()
                logger.debug("Stop event set for assistant thread.")

                if hasattr(self.transcriber, 'stop') and callable(self.transcriber.stop):
                     logger.debug("Calling transcriber.stop()...")
                     try:
                        self.transcriber.stop()
                        logger.info("Transcriber stop method called successfully.")
                     except Exception as stop_ex:
                         logger.exception("Exception occurred while calling transcriber.stop():")
                else:
                     logger.warning("Transcriber object does not have a callable 'stop' method.")

                if self.assistant_thread and self.assistant_thread.is_alive():
                    logger.debug(f"Waiting shortly for assistant thread ({self.assistant_thread.name}) to join...")
                    self.assistant_thread.join(timeout=5.0)
                    if self.assistant_thread.is_alive():
                         logger.warning("Assistant thread did not join within timeout.")
                    else:
                         logger.info("Assistant thread joined successfully.")

                self.is_running = False
                self.assistant_thread = None
                logger.info(f"Assistant stop process completed. Set is_running to {self.is_running}.")
                return control_pb2.Status(message="Assistant stopped successfully")
            else:
                logger.warning("TurnOff request received, but assistant is NOT marked as running (is_running is False).")
                if self.assistant_thread and self.assistant_thread.is_alive():
                    logger.warning(f"Found a potentially orphaned assistant thread ({self.assistant_thread.name}). Setting stop event.")
                    self._stop_event.set()
                return control_pb2.Status(message="Assistant was not running")
        except Exception as e:
            logger.exception("Error processing TurnOff request:")
            return control_pb2.Status(message=f"Error stopping assistant: {str(e)}")



    def _start_assistant(self, mic_name_target, stop_event):
        thread_name = threading.current_thread().name
        logger.info(f"Assistant thread '{thread_name}' started. Target mic: '{mic_name_target}'")
        try:
            if hasattr(self.transcriber, 'start_with_device') and callable(self.transcriber.start_with_device):
                logger.debug(f"Calling blocking transcriber.start_with_device('{mic_name_target}')...")
               
                self.transcriber.start_with_device(mic_name_target)
                logger.info(f"Transcriber method 'start_with_device' finished execution in thread '{thread_name}'.")
            else:
                 logger.error("CRITICAL: Transcriber object does not have a callable 'start_with_device' method.")
                 self.is_running = False 
        except Exception as e:
            logger.exception(f"Exception occurred within assistant thread '{thread_name}' (likely propagating from start_with_device):")
            self.is_running = False
        finally:
            logger.info(f"Assistant thread '{thread_name}' entering finally block (after start_with_device completed).")
            if not stop_event.is_set():
                logger.warning(f"Assistant thread '{thread_name}' exiting because start_with_device finished WITHOUT stop_event. Likely an internal error. Ensuring is_running is False.")
                self.is_running = False
            else:
            
                logger.info(f"Assistant thread '{thread_name}' exiting WITH stop_event set (expected during TurnOff). Let TurnOff handle is_running.")
        
            logger.info(f"Assistant thread '{thread_name}' finished execution (finally block complete).")

def create_port_file(port):
    port_file_path = os.path.join(current_dir, "server_port.txt")
    try:
        with open(port_file_path, "w", encoding='utf-8') as f:
            f.write(str(port))
        logger.info(f"Created port file '{port_file_path}' with port {port}")
    except Exception as e:
         logger.exception(f"Failed to create port file '{port_file_path}'")

def serve():
    logger.info("Starting serve() function...")
    server = None
    transcriber = None
    try:
        try:
             logger.debug("Initializing ContinuousTranscription...")
             transcriber = ContinuousTranscription()
             if transcriber is None:
                  raise RuntimeError("ContinuousTranscription() returned None")
             logger.info("ContinuousTranscription initialized successfully.")
        except Exception as e:
             logger.exception("CRITICAL: Failed to initialize ContinuousTranscription!")
             print(f"FATAL_ERROR: Failed to init transcriber. Check serve_py log. Error: {e}", file=sys.stderr)
             sys.stderr.flush()
             sys.exit("Failed to init transcriber")

        server = grpc.server(
            futures.ThreadPoolExecutor(max_workers=10),
            options=[
                ('grpc.keepalive_time_ms', 10000),
                ('grpc.keepalive_timeout_ms', 5000),
                ('grpc.keepalive_permit_without_calls', True),
                ('grpc.http2.min_ping_interval_without_data_ms', 5000),
                ('grpc.http2.max_pings_without_data', 0)
            ]
        )
        logger.debug("gRPC server object created with keepalive options.")

        servicer = ControlServicer(transcriber)
        logger.debug("ControlServicer instance created.")

        control_pb2_grpc.add_ControlServiceServicer_to_server(servicer, server)
        logger.debug("ControlServiceServicer added to server.")

        max_retries = 10
        success = False
        bound_port = -1
        logger.info(f"Attempting to find and bind an available port (start: 50051, max retries: {max_retries})...")
        for attempt in range(max_retries):
            port_to_try = 50051 + attempt
            server_address = f'[::]:{port_to_try}'
            logger.debug(f"Attempt {attempt + 1}/{max_retries}: Trying port {port_to_try} ({server_address})")

            socket_check_ok = False
            try:
                s = socket.socket(socket.AF_INET6, socket.SOCK_STREAM)
                s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
                s.bind(('::', port_to_try))
                s.close()
                socket_check_ok = True
                logger.debug(f"Port {port_to_try} is available (socket check passed).")
            except OSError as e:
                 if e.errno == socket.errno.EADDRINUSE or (hasattr(e, 'winerror') and e.winerror == 10048):
                     logger.warning(f"Port {port_to_try} is already in use (OS Error: {e}).")
                 else:
                     logger.error(f"OSError occurred while checking port {port_to_try}: {e}")
            except Exception as e:
                 logger.exception(f"Unexpected error while checking port {port_to_try} with socket:")

            if not socket_check_ok:
                time.sleep(0.2)
                continue

            try:
                added_port = server.add_insecure_port(server_address)
                if added_port != port_to_try:
                     logger.warning(f"gRPC server reported binding to port {added_port} instead of requested {port_to_try}. Using {added_port}.")
                     bound_port = added_port
                else:
                     bound_port = port_to_try
                logger.info(f"Successfully added insecure port {server_address} (port {bound_port}) to gRPC server.")
                success = True
                break
            except RuntimeError as e:
                 logger.error(f"gRPC failed to bind to port {port_to_try}: {e}")
                 try: server._state.ports = []
                 except: pass
            except Exception as e:
                 logger.exception(f"Unexpected error while adding port {port_to_try} to gRPC server:")
            time.sleep(0.2)

        if not success:
            logger.critical(f"Failed to bind gRPC server to any port after {max_retries} attempts.")
            print(f"FATAL_ERROR: Failed to bind server port. Check serve_py log.", file=sys.stderr)
            sys.stderr.flush()
            sys.exit(1)

        create_port_file(bound_port)

        logger.info(f"Starting gRPC server on port {bound_port}...")
        server.start()

        print(f"gRPC server started on port {bound_port}")
        sys.stdout.flush()

        logger.info(f"--- Server successfully started on port {bound_port}. Waiting for termination... ---")

        server.wait_for_termination()

    except KeyboardInterrupt:
         logger.info("KeyboardInterrupt received. Shutting down...")
    except SystemExit as e:
         logger.info(f"SystemExit called with code {e.code}. Shutting down...")
         raise
    except Exception as e:
         logger.exception("An unexpected critical exception occurred in the main serve() function:")
         print(f"FATAL_ERROR: Unexpected critical error in serve(). Check serve_py log. Error: {e}", file=sys.stderr)
         sys.stderr.flush()
    finally:
        logger.info("Server shutdown sequence initiated (in finally block)...")
        if server:
            logger.info("Attempting to stop gRPC server...")
            server.stop(grace=5.0)
            logger.info("gRPC server stopped.")
        else:
            logger.info("Server object was None, nothing to stop.")
        logger.info("serve() function finished execution.")


if __name__ == "__main__":
    logger.info(f"Executing script: {__file__}")
    exit_code = 0
    try:
        serve()
        logger.info("Script finished normally.")
    except SystemExit as e:
         logger.warning(f"Script exited with SystemExit code: {e.code}")
         exit_code = e.code if isinstance(e.code, int) else 1
    except Exception as e:
        logger.exception("Unhandled top-level exception caught in __main__:")
        print(f"FATAL_ERROR: Unhandled top-level error. Check serve_py log. Error: {e}", file=sys.stderr)
        sys.stderr.flush()
        exit_code = 1
    finally:
        logger.info(f"Script terminating with exit code: {exit_code}")
        logging.shutdown()
        sys.exit(exit_code)