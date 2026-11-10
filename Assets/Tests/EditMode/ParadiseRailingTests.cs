using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Descender.Tests
{
    public sealed class ParadiseRailingTests
    {
        [TestCase("Assets/Scenes/Paradise/Paradise_Z1_WakingHollow.unity")]
        [TestCase("Assets/Scenes/Paradise/Paradise_Z2_Orchard.unity")]
        public void EveryRunOfBalustradeEndsInAPost(string path)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var all = scene
                    .GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true))
                    .ToList();
                var rails = all.Where(r => r.name == "Balustrade").ToList();
                var posts = all.Where(r => r.name == "Rail Post").ToList();
                Assert.That(rails, Is.Not.Empty);
                Assert.That(posts.Count % 2, Is.Zero, "two posts to a run");
                Assert.That(posts.Count, Is.GreaterThanOrEqualTo(2));
                foreach (var post in posts)
                {
                    var x = post.transform.position.x;
                    var near = rails.Any(r =>
                        Mathf.Abs(r.bounds.min.x - x) < 0.6f || Mathf.Abs(r.bounds.max.x - x) < 0.6f
                    );
                    Assert.That(near, Is.True, $"post at {x} closes no run");
                    Assert.That(post.sortingOrder, Is.GreaterThan(rails[0].sortingOrder), "it closes the run in front");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
