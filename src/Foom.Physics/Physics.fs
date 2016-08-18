﻿namespace Foom.Physics

open Ferop

open System.Numerics

[<Struct>]
type PhysicsWorld =

    val World : nativeint

[<Struct>]
type PhysicsCapsule =

    val RigidBody : nativeint

[<Ferop>]
[<Header("""
#include <btBulletDynamicsCommon.h>
#include <iostream>
""")>]
[<ClangOsx (
    "-DGL_GLEXT_PROTOTYPES -I/usr/local/include/bullet",
    "-F/Library/Frameworks -L/usr/local/lib/ -lBulletCollision -lBulletDynamics -lBulletInverseDynamics -lBulletSoftBody"
)>]
[<Cpp>]
module Physics =

    [<Import; MI(MIO.NoInlining)>]
    let init() : PhysicsWorld =
        C """
    // Build the broadphase
    btBroadphaseInterface* broadphase = new btDbvtBroadphase();

    // Set up the collision configuration and dispatcher
    btDefaultCollisionConfiguration* collisionConfiguration = new btDefaultCollisionConfiguration();
    btCollisionDispatcher* dispatcher = new btCollisionDispatcher(collisionConfiguration);

    // The actual physics solver
    btSequentialImpulseConstraintSolver* solver = new btSequentialImpulseConstraintSolver;

    // The world.
    btDiscreteDynamicsWorld* dynamicsWorld = new btDiscreteDynamicsWorld(dispatcher, broadphase, solver, collisionConfiguration);
    dynamicsWorld->setGravity(btVector3(0, 0, -64));

    Physics_PhysicsWorld world;
    world.World = dynamicsWorld;
    return world;
        """

    [<Import; MI(MIO.NoInlining)>]
    let addCapsule (position: Vector3) (radius: float32) (height: float32) (mass: float32) (inertia: Vector3) (world: PhysicsWorld) : PhysicsCapsule =
        C """
        btCapsuleShape *shape = new btCapsuleShape(radius, height);

        btDefaultMotionState* motionstate = new btDefaultMotionState (btTransform(btQuaternion(0, 0, 0, 1), btVector3(position.X, position.Y, position.Z)));

        btRigidBody* body = new btRigidBody (mass, motionstate, shape, btVector3 (inertia.X, inertia.Y, inertia.Z));

        ((btDiscreteDynamicsWorld*)world.World)->addRigidBody (body);

        Physics_PhysicsCapsule capsule;
        capsule.RigidBody = body;
        return capsule;
        """

    [<Import; MI(MIO.NoInlining)>]
    let step (deltaTime: float32) (world: PhysicsWorld) : unit =
        C """
        btDiscreteDynamicsWorld* dynamicsWorld = ((btDiscreteDynamicsWorld*)world.World);
        dynamicsWorld->stepSimulation (deltaTime, 1, deltaTime);
        """

    [<Import; MI(MIO.NoInlining)>]
    let getCapsulePosition (capsule: PhysicsCapsule) : Vector3 =
        C """
        btTransform trans;
        ((btRigidBody*)capsule.RigidBody)->getMotionState()->getWorldTransform(trans);
        btVector3 origin = trans.getOrigin();
        Physics_Vector3 position;
        position.X = origin.getX();
        position.Y = origin.getY();
        position.Z = origin.getZ();
        return position;
        """
    
    



