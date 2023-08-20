#!/usr/bin/python
import sys
import logging
logging.basicConfig(stream=sys.stderr)
sys.path.insert(0,"/home/goldenpotato/Software/vn.potatox.moe/")

from app import app as application
application.secret_key = ''
