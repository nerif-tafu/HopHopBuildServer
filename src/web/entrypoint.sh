#! /usr/bin/env bash
set -e

RUN_COMMAND="talisker.gunicorn.gevent webapp.app:app --bind $1 --worker-class gevent --name talisker-`hostname`"

if [ "${FLASK_DEBUG,,}" = "true" ] || [ "${FLASK_DEBUG}" = "True" ] || [ "${FLASK_DEBUG}" = "1" ] || [ "${FLASK_DEBUG}" = "yes" ]; then
    RUN_COMMAND="${RUN_COMMAND} --reload --log-level debug --timeout 9999"
fi

${RUN_COMMAND}