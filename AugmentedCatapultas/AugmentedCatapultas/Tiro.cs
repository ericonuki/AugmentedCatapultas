using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

using GoblinXNA;
using GoblinXNA.Graphics;
using GoblinXNA.SceneGraph;
using Model = GoblinXNA.Graphics.Model;
using GoblinXNA.Graphics.Geometry;
using GoblinXNA.Device.Generic;
using GoblinXNA.UI.UI2D;

using GoblinXNA.Device.Capture;
using GoblinXNA.Device.Vision;
using GoblinXNA.Device.Vision.Marker;
using GoblinXNA.Device.Util;
using GoblinXNA.Physics;

using GoblinXNA.Physics.Newton1;

using GoblinXNA.Helpers;
using GoblinXNA.Shaders;

namespace AugmentedCatapultas
{
    class Tiro:Element
    {
        private DateTime lastTimePositionWasUpdated; // Quando foi a última vez que esse negócio foi atualizado na tela? Era bom olhar né?
        public Vector3 _momentum; // Pois não vou mudar a referencia do tiro. Não consigo pensar COMO fazer isso agora. Vai ser sempre em relação ao chão. Eu sei que é só aplicar a matriz de transformação invertida e bla bla bla.
        public Vector3 _position;
        public int mass;
        public GeometryNode obj;
        public TransformNode objTransfNode;
        Scene sc;

        public Tiro(String name,Vector3 inicialPosition, int _mass,int size,Scene scene)
        {
            sc = scene;

            obj = new GeometryNode(name);
            _momentum = new Vector3(0, 0, 0);
            _position = inicialPosition;
            obj.Model = new TexturedSphere(size, 20, 20);
            obj.AddToPhysicsEngine = true;
            obj.Physics.Shape = ShapeType.Sphere;
            obj.Model.ShadowAttribute = ShadowAttribute.ReceiveCast;
            obj.Model.Shader = new SimpleShadowShader(scene.ShadowMap);

            obj.Physics.Interactable = true;
            obj.Physics.Collidable = true;
            obj.Physics.Shape = GoblinXNA.Physics.ShapeType.Sphere;
            obj.Physics.Mass = 30f;
            obj.Physics.MaterialName = "CannonBall";
            obj.Physics.ApplyGravity = false;

            objTransfNode = new TransformNode();
            objTransfNode.Translation = _position;

            Material objMaterial = new Material();
            objMaterial.Diffuse = new Vector4(0, 0.5f, 0, 1);
            objMaterial.Specular = Color.White.ToVector4();
            objMaterial.SpecularPower = 10;

            obj.Material = objMaterial;

            objTransfNode.AddChild(obj);

            mass = _mass;

            lastTimePositionWasUpdated = DateTime.Now;
        }

        public void addMomentum(Vector3 momentum) // Em relação à posição atual do tiro. Novamente, não à posição real dele.
        {
            _momentum += momentum;
        }

        public void addImpulse_using_Force(Vector3 externalForce, int milisseconds) // Quanto tempo a força vai ser aplicada?
        {
            _momentum += (externalForce / mass) * (milisseconds / 1000f);
        }

        public void addImpulse_using_Mass_and_Velocity(int externalMass, Vector3 externalVelocity) //Considerando que a raquete do jogador tem massa infinita, ou seja, não se mexe com força aplicada...
        {
            _momentum += (externalMass * externalVelocity) / mass;
        }

        public override void updatePosition()
        {
            int deltaT = (DateTime.Now - lastTimePositionWasUpdated).Milliseconds;

            _position += _momentum * deltaT;

            Matrix mat = Matrix.CreateTranslation(_position);

            // Modify the transformation in the physics engine
            ((NewtonPhysics)sc.PhysicsEngine).SetTransform(obj.Physics, mat);

            lastTimePositionWasUpdated = DateTime.Now;
        }
    }
}
