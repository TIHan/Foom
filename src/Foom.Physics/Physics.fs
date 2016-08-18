﻿namespace Foom.Physics

open Ferop

open System.Numerics

[<Struct>]
type PhysicsWorld =

    val World : nativeint

[<Struct>]
type KinematicController =

    val Ptr : nativeint
    val GhostObjectPtr : nativeint

[<Ferop>]
[<Header("""
#include <btBulletCollisionCommon.h>
#include <btBulletDynamicsCommon.h>
#include <BulletDynamics/Character/btKinematicCharacterController.h>
#include <BulletCollision/CollisionDispatch/btGhostObject.h>
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
        dynamicsWorld->setGravity(btVector3(0, 0, -1024));

        Physics_PhysicsWorld world;
        world.World = dynamicsWorld;
        return world;
        """

    [<Import; MI(MIO.NoInlining)>]
    let addCapsuleController (position: Vector3) (radius: float32) (height: float32) (pworld: PhysicsWorld) : KinematicController =
        C """
        btPairCachingGhostObject* ghostBody;
        btKinematicCharacterController* characterController;

        //create the shape and the properties for the player
        btTransform t;
        t.setIdentity();
        t.setOrigin(btVector3(position.X,position.Y,position.Z));

        btConvexShape *capsule = new btCapsuleShapeZ(radius, height);
        btDiscreteDynamicsWorld* world = ((btDiscreteDynamicsWorld*)pworld.World);

        //btConvexShape* capsule = new btCylinderShape(btVector3(PLAYER_CAPSULE_RADIUS,PLAYER_CAPSULE_HEIGHT/2,PLAYER_CAPSULE_RADIUS));
        
        ghostBody = new btPairCachingGhostObject();
        ghostBody->setWorldTransform(t);
        ghostBody->setCollisionShape(capsule);
        
        ghostBody->setCollisionFlags (btCollisionObject::CF_CHARACTER_OBJECT);
     
        //alocate the character object
        characterController = new btKinematicCharacterController(ghostBody,capsule,btScalar(24));
        characterController->setUseGhostSweepTest(false);
       // characterController->setGravity(0);
        
        world->addCollisionObject(ghostBody, btBroadphaseProxy::CharacterFilter, btBroadphaseProxy::StaticFilter|btBroadphaseProxy::DefaultFilter);
        world->addAction(characterController);

        Physics_KinematicController controller;
        controller.Ptr = characterController;
        controller.GhostObjectPtr = ghostBody;
        return controller;
        """

    [<Import; MI(MIO.NoInlining)>]
    let step (deltaTime: float32) (world: PhysicsWorld) : unit =
        C """
        btDiscreteDynamicsWorld* dynamicsWorld = ((btDiscreteDynamicsWorld*)world.World);
        dynamicsWorld->stepSimulation (deltaTime, 1, deltaTime);
        """

    [<Import; MI(MIO.NoInlining)>]
    let getKinematicControllerPosition (controller: KinematicController) : Vector3 =
        C """
        btTransform trans = ((btPairCachingGhostObject*)controller.GhostObjectPtr)->getWorldTransform();
        btVector3 origin = trans.getOrigin();
        Physics_Vector3 position;
        position.X = origin.getX();
        position.Y = origin.getY();
        position.Z = origin.getZ();
        return position;
        """

    [<Import; MI(MIO.NoInlining)>]
    let preStepKinematicController (controller: KinematicController) (pworld: PhysicsWorld) : unit =
        C """
        ((btKinematicCharacterController*)controller.Ptr)->preStep (((btDiscreteDynamicsWorld*)pworld.World));
        """

    [<Import; MI(MIO.NoInlining)>]
    let stepKinematicController (deltaTime: float32) (controller: KinematicController) (pworld: PhysicsWorld) : unit =
        C """
        ((btKinematicCharacterController*)controller.Ptr)->playerStep (((btDiscreteDynamicsWorld*)pworld.World), deltaTime);
        """


    [<Import; MI(MIO.NoInlining)>]
    let addTriangles (vertices: Vector3 []) (length: int) (world: PhysicsWorld) : unit =
        C """
        btTriangleMesh* trimesh = new btTriangleMesh();

        for (int i = 0; i < length; i = i + 3)
        {
            btVector3 v1 = btVector3(vertices[i].X, vertices[i].Y, vertices[i].Z);
            btVector3 v2 = btVector3(vertices[i + 1].X, vertices[i + 1].Y, vertices[i + 1].Z);
            btVector3 v3 = btVector3(vertices[i + 2].X, vertices[i + 2].Y, vertices[i + 2].Z);
            trimesh->addTriangle (v1, v2, v3);
        }

        btTransform   trans;
        trans.setIdentity();

        btCollisionShape* trimeshShape  = new btBvhTriangleMeshShape( trimesh, true );

        btRigidBody* body= new btRigidBody( 0, 0, trimeshShape, btVector3(0,0,0) );
        ((btDiscreteDynamicsWorld*)world.World)->addRigidBody( body );
        """
    
    



