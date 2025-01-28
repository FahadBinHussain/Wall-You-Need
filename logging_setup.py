import logging
import queue

class TextHandler(logging.Handler):
    """Class to handle logging messages and display them in a Tkinter Text widget."""
    def __init__(self, text_widget, log_queue):
        logging.Handler.__init__(self)
        self.text_widget = text_widget
        self.log_queue = log_queue

    def emit(self, record):
        msg = self.format(record)
        self.log_queue.put(msg)