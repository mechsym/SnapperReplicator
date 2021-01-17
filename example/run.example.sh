#!/usr/bin/env bash
set -eu.o pipefail

EXE_PATH=snapper-replicator
REMOTE_USER=root
REMOTE_HOST=example.com
SSH_KEY=/home/foo/.ssh/id_rsa
REMOTE_CONFIG=root
LOCAL_CONFIG=root
REMOTE_WORKING_DIRECTORY=/tmp/snapper-replicator
LOCAL_WORKING_DIRECTORY=/tmp/snapper-replicator
OPERATION_MODE=pull
TRANSFER_MODE=rsync
SNAPPER_CLEANUP_ALGO=timeline

# create workdirs on local/remote
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/workdirs.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  create-workdirs
else
  echo "Workdirs exist"
fi

# run `snapper clean` on source
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/snapper-cleanup.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  snapper-cleanup-source \
  -alg $SNAPPER_CLEANUP_ALGO
else
  echo "Snapper cleanup was run"
fi

# determine changes
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/pending_changes.json" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  determine-changes
else
  echo "Changes are precomputed"
fi

# dump snapshots on source to the workdir
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/dump.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  dump \
  -m incremental
else
  echo "Dumps exist"
fi

# transfer snapshots to destination
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/sync.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  synchronize \
  -m $TRANSFER_MODE
else
  echo "Sync was done"
fi

# restore snapshots on destination
if [[ ! -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/restore.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  restore
else
  echo "Restore was done"
fi
  
# cleanup remote workdir  
# cleanup local workdir
if [[ -f "$LOCAL_WORKING_DIRECTORY/$LOCAL_CONFIG/restore.done" ]]; then
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  clean-remote-workdir
  
  $EXE_PATH -u $REMOTE_USER \
  -h $REMOTE_HOST \
  -k $SSH_KEY \
  -rc $REMOTE_CONFIG \
  -lc $LOCAL_CONFIG \
  -rwd $REMOTE_WORKING_DIRECTORY \
  -lwd $LOCAL_WORKING_DIRECTORY  \
  -m $OPERATION_MODE \
  clean-local-workdir  
else
  echo "Restore was done"
fi