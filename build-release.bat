docker build -t poweroutagenotifier .
docker tag poweroutagenotifier belgradebc/poweroutagenotifier
docker save -o poweroutagenotifier.tar belgradebc/poweroutagenotifier